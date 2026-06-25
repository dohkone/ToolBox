const { existsSync, readdirSync, readFileSync } = require('fs');
const path = require('path');
const { spawn } = require('child_process');

function parseArgs(argv) {
  const result = {};
  for (let index = 2; index < argv.length; index += 1) {
    const current = argv[index];
    if (current === '--config') {
      result.config = argv[index + 1];
      index += 1;
    }
  }
  return result;
}

function readConfig(configPath) {
  if (!configPath || !existsSync(configPath)) {
    throw new Error(`Missing Yingdao config: ${configPath || '(empty path)'}`);
  }

  return JSON.parse(readFileSync(configPath, 'utf8'));
}

function findServerPath(npxRoot) {
  if (!existsSync(npxRoot)) {
    throw new Error(`Cannot find npm npx cache: ${npxRoot}. Run: npx -y yingdao-mcp-server`);
  }

  for (const entry of readdirSync(npxRoot, { withFileTypes: true })) {
    if (!entry.isDirectory()) {
      continue;
    }

    const candidate = path.join(
      npxRoot,
      entry.name,
      'node_modules',
      'yingdao-mcp-server',
      'dist',
      'index.js'
    );

    if (existsSync(candidate)) {
      return candidate;
    }
  }

  throw new Error('Cannot find yingdao-mcp-server in npm npx cache. Run: npx -y yingdao-mcp-server');
}

function findLocalAppFallback(config) {
  const appsDir = path.join(config.userFolder, 'apps');
  if (!existsSync(appsDir)) {
    return null;
  }

  for (const entry of readdirSync(appsDir, { withFileTypes: true })) {
    if (!entry.isDirectory() || !entry.name.endsWith('_Release')) {
      continue;
    }

    const packagePath = path.join(appsDir, entry.name, 'xbot_robot', 'package.json');
    if (!existsSync(packagePath)) {
      continue;
    }

    try {
      const packageData = JSON.parse(readFileSync(packagePath, 'utf8'));
      if (
        packageData.robot_type === 'app' &&
        (packageData.uuid === config.appUuid || String(packageData.name || '').includes(config.appNameKeyword))
      ) {
        return {
          uuid: packageData.uuid || config.appUuid,
          name: packageData.name || config.appName,
          description: packageData.description || ''
        };
      }
    } catch {
      // Ignore malformed package metadata and continue scanning.
    }
  }

  return null;
}

async function main() {
  const args = parseArgs(process.argv);
  const config = readConfig(args.config);
  const serverPath = findServerPath(config.npxRoot);

  const child = spawn(process.execPath, [serverPath], {
    env: {
      ...process.env,
      RPA_MODEL: 'local',
      USER_FOLDER: config.userFolder,
      SHADOWBOT_PATH: config.shadowBotPath,
      LANGUAGE: 'zh'
    },
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true
  });

  let nextId = 1;
  const pending = new Map();
  let buffer = '';

  function send(method, params = {}) {
    const id = nextId++;
    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, params }) + '\n');

    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        pending.delete(id);
        reject(new Error(`Timeout waiting for ${method}`));
      }, 30000);

      pending.set(id, { resolve, reject, timer });
    });
  }

  function notify(method, params = {}) {
    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', method, params }) + '\n');
  }

  child.stdout.on('data', chunk => {
    buffer += chunk.toString('utf8');
    let newline;

    while ((newline = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, newline).trim();
      buffer = buffer.slice(newline + 1);
      if (!line) {
        continue;
      }

      let message;
      try {
        message = JSON.parse(line);
      } catch {
        continue;
      }

      const waiter = pending.get(message.id);
      if (!waiter) {
        continue;
      }

      clearTimeout(waiter.timer);
      pending.delete(message.id);

      if (message.error) {
        waiter.reject(new Error(JSON.stringify(message.error)));
      } else {
        waiter.resolve(message.result);
      }
    }
  });

  child.stderr.on('data', () => {
    // Keep stderr out of the user-visible black console path; errors surface on non-zero exit.
  });

  child.on('exit', code => {
    for (const [, waiter] of pending) {
      waiter.reject(new Error(`server exited with code ${code}`));
    }
    pending.clear();
  });

  try {
    await send('initialize', {
      protocolVersion: '2024-11-05',
      capabilities: {},
      clientInfo: { name: 'imagekeeper-yingdao-launcher', version: '1.0.0' }
    });
    notify('notifications/initialized');

    const list = await send('tools/call', { name: 'queryApplist', arguments: {} });
    const text = list.content?.find(item => item.type === 'text')?.text || '[]';
    const apps = JSON.parse(text);
    const target =
      apps.find(app => app.uuid === config.appUuid) ||
      apps.find(app => String(app.name || '').includes(config.appNameKeyword)) ||
      findLocalAppFallback(config);

    if (!target) {
      throw new Error(`Cannot find ${config.appName}.`);
    }

    const result = await send('tools/call', {
      name: 'runApp',
      arguments: { appUuid: target.uuid, appParams: {} }
    });

    console.log(JSON.stringify({
      started: true,
      message: `${config.appName}已启动`,
      target,
      result
    }));

    child.stdin.end();
    setTimeout(() => child.kill(), 1000);
  } catch (error) {
    try {
      child.kill();
    } catch {
      // Process may already be gone.
    }

    throw error;
  }
}

main().catch(error => {
  console.error(error.stack || String(error));
  process.exitCode = 1;
});
