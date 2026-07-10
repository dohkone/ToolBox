using System.Windows;
using System.Windows.Documents;
using WpfControls = System.Windows.Controls;
using WpfInput = System.Windows.Input;
using WpfMedia = System.Windows.Media;

namespace ImageKeeper.App.Controls;

public sealed class PlaceholderHighlightTextBox : WpfControls.RichTextBox
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(PlaceholderHighlightTextBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTextChanged));

    private static readonly Dictionary<string, string> PlaceholderTips = new()
    {
        ["{场景}"] = "生成时会替换为随机场景模板",
        ["{主体}"] = "生成时会替换为该场景绑定的主体",
        ["{颜色}"] = "生成时会从颜色池随机替换",
        ["{唯一颜色}"] = "每张提示词开始生成时先固定 1 个颜色，后续所有“唯一颜色”都复用这个值",
        ["{固定颜色}"] = "每张提示词开始生成时先固定 1 个颜色，后续所有“固定颜色”都复用这个值",
        ["{唯一主体}"] = "每张提示词开始生成时先固定 1 个主体，后续所有“唯一主体”都复用这个值",
        ["{固定主体}"] = "每张提示词开始生成时先固定 1 个主体，后续所有“固定主体”都复用这个值",
        ["{全部颜色}"] = "生成时会展开为颜色列表里的全部内容",
        ["{全部主体}"] = "生成时会展开为主体列表里的全部内容",
    };

    private bool _isInternalUpdate;

    public PlaceholderHighlightTextBox()
    {
        AcceptsTab = true;
        BorderBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(214, 220, 232));
        BorderThickness = new Thickness(1);
        Background = WpfMedia.Brushes.White;
        Padding = new Thickness(10, 8, 10, 8);
        VerticalScrollBarVisibility = WpfControls.ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = WpfControls.ScrollBarVisibility.Disabled;

        _isInternalUpdate = true;
        try
        {
            Document = CreateDocument(string.Empty);
        }
        finally
        {
            _isInternalUpdate = false;
        }
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnTextChanged(WpfControls.TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        if (_isInternalUpdate)
        {
            return;
        }

        SetCurrentValue(TextProperty, GetDocumentText());
    }

    protected override void OnLostKeyboardFocus(WpfInput.KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        RebuildDocument(GetDocumentText(), GetCaretOffset());
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (PlaceholderHighlightTextBox)d;
        if (editor._isInternalUpdate)
        {
            return;
        }

        var newText = e.NewValue as string ?? string.Empty;
        if (editor.GetDocumentText() == newText)
        {
            return;
        }

        editor.RebuildDocument(newText, newText.Length);
    }

    private void RebuildDocument(string text, int caretOffset)
    {
        _isInternalUpdate = true;
        try
        {
            Document = CreateDocument(text);
            CaretPosition = GetTextPointerAtOffset(Document.ContentStart, Math.Min(caretOffset, text.Length))
                ?? Document.ContentEnd;
        }
        finally
        {
            _isInternalUpdate = false;
        }
    }

    private static FlowDocument CreateDocument(string text)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new WpfMedia.FontFamily("Microsoft YaHei UI"),
            FontSize = 13,
            LineHeight = 20
        };

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0)
        };

        var index = 0;
        while (index < text.Length)
        {
            var match = FindNextPlaceholder(text, index);
            if (match.Index < 0)
            {
                paragraph.Inlines.Add(new Run(text[index..]));
                break;
            }

            if (match.Index > index)
            {
                paragraph.Inlines.Add(new Run(text[index..match.Index]));
            }

            paragraph.Inlines.Add(CreatePlaceholderInline(match.Placeholder));
            index = match.Index + match.Placeholder.Length;
        }

        if (text.Length == 0)
        {
            paragraph.Inlines.Add(new Run(string.Empty));
        }

        document.Blocks.Add(paragraph);
        return document;
    }

    private static Span CreatePlaceholderInline(string placeholder)
    {
        var span = new Span(new Run(placeholder))
        {
            Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(230, 244, 255)),
            Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(45, 106, 227)),
            Cursor = WpfInput.Cursors.Help
        };

        WpfControls.ToolTipService.SetToolTip(
            span,
            PlaceholderTips.TryGetValue(placeholder, out var tip)
                ? tip
                : "生成时会替换为对应内容");
        WpfControls.ToolTipService.SetInitialShowDelay(span, 200);
        WpfControls.ToolTipService.SetShowDuration(span, 8000);
        return span;
    }

    private static (int Index, string Placeholder) FindNextPlaceholder(string text, int startIndex)
    {
        var bestIndex = -1;
        var bestPlaceholder = string.Empty;

        foreach (var placeholder in PlaceholderTips.Keys)
        {
            var index = text.IndexOf(placeholder, startIndex, StringComparison.Ordinal);
            if (index >= 0 && (bestIndex < 0 || index < bestIndex))
            {
                bestIndex = index;
                bestPlaceholder = placeholder;
            }
        }

        return (bestIndex, bestPlaceholder);
    }

    private string GetDocumentText()
    {
        var text = new TextRange(Document.ContentStart, Document.ContentEnd).Text;
        return text.EndsWith("\r\n", StringComparison.Ordinal)
            ? text[..^2]
            : text;
    }

    private int GetCaretOffset()
    {
        var text = new TextRange(Document.ContentStart, CaretPosition).Text;
        return text.EndsWith("\r\n", StringComparison.Ordinal)
            ? text.Length - 2
            : text.Length;
    }

    private static TextPointer? GetTextPointerAtOffset(TextPointer start, int offset)
    {
        var current = start;
        var remaining = offset;

        while (current is not null)
        {
            if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = current.GetTextInRun(LogicalDirection.Forward);
                if (remaining <= text.Length)
                {
                    return current.GetPositionAtOffset(remaining, LogicalDirection.Forward);
                }

                remaining -= text.Length;
            }

            current = current.GetNextContextPosition(LogicalDirection.Forward);
        }

        return null;
    }
}
