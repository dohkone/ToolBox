using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace ImageKeeper.App.Controls;

public sealed class PlaceholderHighlightTextBox : RichTextBox
{
	public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(PlaceholderHighlightTextBox), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

	private static readonly Dictionary<string, string> PlaceholderTips = new Dictionary<string, string>
	{
		["{场景}"] = "生成时会替换为随机场景模板",
		["{主体}"] = "生成时会替换为该场景绑定的主体",
		["{颜色}"] = "生成时会从颜色池随机替换",
		["{唯一颜色}"] = "每张提示词开始生成时先固定 1 个颜色，后续所有“唯一颜色”都复用这个值",
		["{固定颜色}"] = "每张提示词开始生成时先固定 1 个颜色，后续所有“固定颜色”都复用这个值",
		["{唯一主体}"] = "每张提示词开始生成时先固定 1 个主体，后续所有“唯一主体”都复用这个值",
		["{固定主体}"] = "每张提示词开始生成时先固定 1 个主体，后续所有“固定主体”都复用这个值",
		["{全部颜色}"] = "生成时会展开为颜色列表里的全部内容",
		["{全部主体}"] = "生成时会展开为主体列表里的全部内容"
	};

	private bool _isInternalUpdate;

	public string Text
	{
		get
		{
			return (string)GetValue(TextProperty);
		}
		set
		{
			SetValue(TextProperty, value);
		}
	}

	public PlaceholderHighlightTextBox()
	{
		base.AcceptsTab = true;
		base.BorderBrush = new SolidColorBrush(Color.FromRgb(214, 220, 232));
		base.BorderThickness = new Thickness(1.0);
		base.Background = Brushes.White;
		base.Padding = new Thickness(10.0, 8.0, 10.0, 8.0);
		base.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		base.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		_isInternalUpdate = true;
		try
		{
			base.Document = CreateDocument(string.Empty);
		}
		finally
		{
			_isInternalUpdate = false;
		}
	}

	protected override void OnTextChanged(TextChangedEventArgs e)
	{
		base.OnTextChanged(e);
		if (!_isInternalUpdate)
		{
			SetCurrentValue(TextProperty, GetDocumentText());
		}
	}

	protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
	{
		base.OnLostKeyboardFocus(e);
		RebuildDocument(GetDocumentText(), GetCaretOffset());
	}

	private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		PlaceholderHighlightTextBox placeholderHighlightTextBox = (PlaceholderHighlightTextBox)d;
		if (!placeholderHighlightTextBox._isInternalUpdate)
		{
			string text = (e.NewValue as string) ?? string.Empty;
			if (!(placeholderHighlightTextBox.GetDocumentText() == text))
			{
				placeholderHighlightTextBox.RebuildDocument(text, text.Length);
			}
		}
	}

	private void RebuildDocument(string text, int caretOffset)
	{
		_isInternalUpdate = true;
		try
		{
			base.Document = CreateDocument(text);
			base.CaretPosition = GetTextPointerAtOffset(base.Document.ContentStart, Math.Min(caretOffset, text.Length)) ?? base.Document.ContentEnd;
		}
		finally
		{
			_isInternalUpdate = false;
		}
	}

	private static FlowDocument CreateDocument(string text)
	{
		FlowDocument flowDocument = new FlowDocument
		{
			PagePadding = new Thickness(0.0),
			FontFamily = new FontFamily("Microsoft YaHei UI"),
			FontSize = 13.0,
			LineHeight = 20.0
		};
		Paragraph paragraph = new Paragraph
		{
			Margin = new Thickness(0.0)
		};
		int num = 0;
		while (num < text.Length)
		{
			(int, string) tuple = FindNextPlaceholder(text, num);
			if (tuple.Item1 < 0)
			{
				InlineCollection inlines = paragraph.Inlines;
				int num2 = num;
				inlines.Add(new Run(text.Substring(num2, text.Length - num2)));
				break;
			}
			if (tuple.Item1 > num)
			{
				InlineCollection inlines2 = paragraph.Inlines;
				int num2 = num;
				inlines2.Add(new Run(text.Substring(num2, tuple.Item1 - num2)));
			}
			paragraph.Inlines.Add(CreatePlaceholderInline(tuple.Item2));
			num = tuple.Item1 + tuple.Item2.Length;
		}
		if (text.Length == 0)
		{
			paragraph.Inlines.Add(new Run(string.Empty));
		}
		flowDocument.Blocks.Add(paragraph);
		return flowDocument;
	}

	private static Span CreatePlaceholderInline(string placeholder)
	{
		Span obj = new Span(new Run(placeholder))
		{
			Background = new SolidColorBrush(Color.FromRgb(230, 244, byte.MaxValue)),
			Foreground = new SolidColorBrush(Color.FromRgb(45, 106, 227)),
			Cursor = Cursors.Help
		};
		ToolTipService.SetToolTip(obj, PlaceholderTips.TryGetValue(placeholder, out string value) ? value : "生成时会替换为对应内容");
		ToolTipService.SetInitialShowDelay(obj, 200);
		ToolTipService.SetShowDuration(obj, 8000);
		return obj;
	}

	private static (int Index, string Placeholder) FindNextPlaceholder(string text, int startIndex)
	{
		int num = -1;
		string item = string.Empty;
		foreach (string key in PlaceholderTips.Keys)
		{
			int num2 = text.IndexOf(key, startIndex, StringComparison.Ordinal);
			if (num2 >= 0 && (num < 0 || num2 < num))
			{
				num = num2;
				item = key;
			}
		}
		return (Index: num, Placeholder: item);
	}

	private string GetDocumentText()
	{
		string text = new TextRange(base.Document.ContentStart, base.Document.ContentEnd).Text;
		if (!text.EndsWith("\r\n", StringComparison.Ordinal))
		{
			return text;
		}
		string text2 = text;
		return text2.Substring(0, text2.Length - 2);
	}

	private int GetCaretOffset()
	{
		string text = new TextRange(base.Document.ContentStart, base.CaretPosition).Text;
		if (!text.EndsWith("\r\n", StringComparison.Ordinal))
		{
			return text.Length;
		}
		return text.Length - 2;
	}

	private static TextPointer? GetTextPointerAtOffset(TextPointer start, int offset)
	{
		TextPointer textPointer = start;
		int num = offset;
		while (textPointer != null)
		{
			if (textPointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
			{
				string textInRun = textPointer.GetTextInRun(LogicalDirection.Forward);
				if (num <= textInRun.Length)
				{
					return textPointer.GetPositionAtOffset(num, LogicalDirection.Forward);
				}
				num -= textInRun.Length;
			}
			textPointer = textPointer.GetNextContextPosition(LogicalDirection.Forward);
		}
		return null;
	}
}
