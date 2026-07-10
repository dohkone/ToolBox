using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ImageKeeper.App.Utilities;
using ImageKeeper.Core.Models;

namespace ImageKeeper.App.ViewModels;

public sealed class TemplateItemViewModel : ViewModelBase
{
	private bool _isSelected;

	private string? _subjectSummaryOverride;

	public TemplateItemRecord Model { get; private set; }

	public long Id => Model.Id;

	public TemplateCategory Category => Model.Category;

	public string Name => Model.Name;

	public string Content => Model.Content;

	public string Subject => Model.Subject;

	public string PreviewImagePath => Model.PreviewImagePath;

	public ImageTemplateType ImageType => Model.ImageType;

	public string ImageTypeText => ImageType switch
	{
		ImageTemplateType.SceneImage => "场景图", 
		ImageTemplateType.CompareImage => "对比图", 
		_ => "主图", 
	};

	public bool HasPreviewImage
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(PreviewImagePath))
			{
				return File.Exists(PreviewImagePath);
			}
			return false;
		}
	}

	public bool IsEnabled => Model.IsEnabled;

	public string EnabledText
	{
		get
		{
			if (!IsEnabled)
			{
				return "停用";
			}
			return "启用";
		}
	}

	public string EnabledBadgeBackground
	{
		get
		{
			if (!IsEnabled)
			{
				return "#909399";
			}
			return "#67C23A";
		}
	}

	public string EnabledBadgeBorder
	{
		get
		{
			if (!IsEnabled)
			{
				return "#909399";
			}
			return "#67C23A";
		}
	}

	public string EnabledBadgeForeground => "White";

	public string CreatedAtText => Model.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

	public string UpdatedAtText => Model.UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

	public string ContentSummary
	{
		get
		{
			string text = Content.ReplaceLineEndings(" ").Trim();
			if (text.Length > 80)
			{
				return text.Substring(0, 80) + "...";
			}
			return text;
		}
	}

	public string SubjectSummary
	{
		get
		{
			string text = (_subjectSummaryOverride ?? Subject).ReplaceLineEndings(" ").Trim();
			if (!string.IsNullOrWhiteSpace(text))
			{
				if (text.Length > 60)
				{
					return text.Substring(0, 60) + "...";
				}
				return text;
			}
			return "-";
		}
	}

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			SetProperty(ref _isSelected, value, "IsSelected");
		}
	}

	public ICommand OpenPreviewImageCommand { get; }

	public TemplateItemViewModel(TemplateItemRecord model, string? subjectSummaryOverride = null)
	{
		Model = model;
		_subjectSummaryOverride = subjectSummaryOverride;
		OpenPreviewImageCommand = new RelayCommand(delegate
		{
			OpenPreviewImage();
		}, (object? _) => HasPreviewImage);
	}

	public void Update(TemplateItemRecord model, string? subjectSummaryOverride = null)
	{
		Model = model;
		_subjectSummaryOverride = subjectSummaryOverride;
		OnPropertyChanged("Id");
		OnPropertyChanged("Category");
		OnPropertyChanged("Name");
		OnPropertyChanged("Content");
		OnPropertyChanged("Subject");
		OnPropertyChanged("PreviewImagePath");
		OnPropertyChanged("ImageType");
		OnPropertyChanged("ImageTypeText");
		OnPropertyChanged("HasPreviewImage");
		OnPropertyChanged("IsEnabled");
		OnPropertyChanged("EnabledText");
		OnPropertyChanged("EnabledBadgeBackground");
		OnPropertyChanged("EnabledBadgeBorder");
		OnPropertyChanged("EnabledBadgeForeground");
		OnPropertyChanged("CreatedAtText");
		OnPropertyChanged("UpdatedAtText");
		OnPropertyChanged("ContentSummary");
		OnPropertyChanged("SubjectSummary");
	}

	private void OpenPreviewImage()
	{
		if (!HasPreviewImage)
		{
			return;
		}
		try
		{
			ShellOpenHelper.OpenFile(PreviewImagePath);
		}
		catch (Exception ex)
		{
			MessageBox.Show("打开预览图失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}
}
