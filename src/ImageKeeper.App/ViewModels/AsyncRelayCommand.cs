using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ImageKeeper.App.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
	private readonly Func<object?, Task> _executeAsync;

	private readonly Predicate<object?>? _canExecute;

	private bool _isExecuting;

	public event EventHandler? CanExecuteChanged;

	public AsyncRelayCommand(Func<object?, Task> executeAsync, Predicate<object?>? canExecute = null)
	{
		_executeAsync = executeAsync;
		_canExecute = canExecute;
	}

	public bool CanExecute(object? parameter)
	{
		if (!_isExecuting)
		{
			return _canExecute?.Invoke(parameter) ?? true;
		}
		return false;
	}

	public async void Execute(object? parameter)
	{
		if (!CanExecute(parameter))
		{
			return;
		}
		try
		{
			_isExecuting = true;
			RaiseCanExecuteChanged();
			await _executeAsync(parameter);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "操作失败", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			_isExecuting = false;
			RaiseCanExecuteChanged();
		}
	}

	public void RaiseCanExecuteChanged()
	{
		this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
	}
}
