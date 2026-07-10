namespace ImageKeeper.App.ViewModels;

public sealed class WorkspaceViewModel : ViewModelBase
{
	private string _rootFolder = string.Empty;

	private string _backupFolder = string.Empty;

	public string RootFolder
	{
		get
		{
			return _rootFolder;
		}
		set
		{
			SetProperty(ref _rootFolder, value, "RootFolder");
		}
	}

	public string BackupFolder
	{
		get
		{
			return _backupFolder;
		}
		set
		{
			SetProperty(ref _backupFolder, value, "BackupFolder");
		}
	}
}
