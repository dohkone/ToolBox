# Playwright TS Miaoshou

This project uses TypeScript + Playwright and defaults to `Edge Stable`.

## Setup

```powershell
cd C:\Users\Administrator\Documents\Codex\playwright-ts-miaoshou
copy .env.example .env
```

Default `.env`:

```env
BASE_URL=https://erp.91miaoshou.com/welcome
BROWSER_CHANNEL=msedge
```

## Open Miaoshou

```powershell
npm run open:miaoshou
```

What this script does:

- opens Miaoshou ERP in `Edge Stable`
- checks whether a visible `立即登录` button exists
- if the login page is detected, saves a screenshot to `output\miaoshou-login.png`
- keeps the browser open so you can log in manually

## Other commands

```powershell
npm run login
npm run test
npm run test:chrome
npm run test:edge
npm run codegen
```
