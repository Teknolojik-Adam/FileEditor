using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace dosya_duzenleme
{
    public static class RichTextBoxBinder
    {
        public static readonly DependencyProperty DocumentProperty =
            DependencyProperty.RegisterAttached(
                "Document",
                typeof(FlowDocument),
                typeof(RichTextBoxBinder),
                new FrameworkPropertyMetadata(null, OnDocumentChanged));

        public static FlowDocument GetDocument(DependencyObject dp)
        {
            return (FlowDocument)dp.GetValue(DocumentProperty);
        }

        public static void SetDocument(DependencyObject dp, FlowDocument value)
        {
            dp.SetValue(DocumentProperty, value);
        }

        private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var rtb = (RichTextBox)d;
            rtb.Document = (FlowDocument)e.NewValue;
        }
    }

    public class DocumentTab : INotifyPropertyChanged
    {
        private string _header;
        public string Header
        {
            get { return _header; }
            set
            {
                if (_header != value)
                {
                    _header = value;
                    OnPropertyChanged(nameof(Header));
                }
            }
        }

        public string FilePath { get; set; }
        public FlowDocument Content { get; set; }
        public bool IsDirty { get; set; }

        [Bindable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public RichTextBox Editor { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public DocumentTab()
        {
            Content = new FlowDocument();
            IsDirty = false;
        }
    }

    public partial class MainWindow : Window
    {
        private ResourceManager? _rm;
        private ObservableCollection<DocumentTab> _documentTabs = new ObservableCollection<DocumentTab>();

        public MainWindow()
        {
            InitializeComponent();
            tabControl.ItemsSource = _documentTabs;

            try
            {
                _rm = new ResourceManager("dosya_duzenleme.Resources", typeof(MainWindow).Assembly);
            }
            catch
            {
                _rm = null;
            }

         
            ApplyLanguage(new CultureInfo("en"));

            AddNewTab();
        }

        private void AddNewTab(string filePath = null)
        {
            var culture = CultureInfo.CurrentUICulture;
            var newTab = new DocumentTab();

            if (string.IsNullOrEmpty(filePath))
            {
                newTab.Header = GetStringOrFallback("Title_NewDocument", culture, "New Document");
                newTab.FilePath = null;
            }
            else
            {
                newTab.Header = Path.GetFileName(filePath);
                newTab.FilePath = filePath;
            }

            _documentTabs.Add(newTab);
            tabControl.SelectedItem = newTab;
        }

        private void UpdateWindowTitle()
        {
            var culture = CultureInfo.CurrentUICulture;
            var titleEditor = GetStringOrFallback("Title_Editor", culture, "Text Editor");

            if (tabControl.SelectedItem is DocumentTab currentTab && currentTab != null)
            {
                Title = $"{titleEditor} - {currentTab.Header}";
            }
            else
            {
                Title = titleEditor;
            }
        }

        private bool CheckUnsavedChanges()
        {
            if (tabControl.SelectedItem is DocumentTab currentTab && currentTab.IsDirty)
            {
                var culture = CultureInfo.CurrentUICulture;
                var message = GetStringOrFallback("Msg_UnsavedChanges", culture, "There are unsaved changes. Do you want to save them?");
                var caption = GetStringOrFallback("Title_Warning", culture, "Warning");
                var result = MessageBox.Show(message, caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                return HandleSaveDialogResult(result);
            }
            return false;
        }

        private bool HandleSaveDialogResult(MessageBoxResult result)
        {
            switch (result)
            {
                case MessageBoxResult.Yes:
                    SaveFile(false);
                    return false;
                case MessageBoxResult.No:
                    return false;
                default:
                    return true;
            }
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            AddNewTab();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
            {
                AddNewTab(dlg.FileName);
                LoadFileContent(tabControl.SelectedItem as DocumentTab);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => SaveFile(false);

        private void SaveFile(bool saveAs)
        {
            if (!(tabControl.SelectedItem is DocumentTab currentTab)) return;

            if (saveAs || string.IsNullOrEmpty(currentTab.FilePath))
            {
                var dlg = new SaveFileDialog();
                if (dlg.ShowDialog() != true) return;
                currentTab.FilePath = dlg.FileName;
                currentTab.Header = Path.GetFileName(currentTab.FilePath);
            }

            try
            {
                using (var writer = new StreamWriter(currentTab.FilePath))
                {
                    var textRange = new TextRange(currentTab.Content.ContentStart, currentTab.Content.ContentEnd);
                    textRange.Save(writer.BaseStream, DataFormats.Text);
                }

                currentTab.IsDirty = false;
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                var culture = CultureInfo.CurrentUICulture;
                var message = string.Format(GetStringOrFallback("Msg_FileSaveError", culture, "File could not be saved: {0}"), ex.Message);
                var caption = GetStringOrFallback("Title_Error", culture, "Error");
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFileContent(DocumentTab tab)
        {
            if (tab == null || string.IsNullOrEmpty(tab.FilePath)) return;

            try
            {
                using (var reader = new StreamReader(tab.FilePath))
                {
                    var textRange = new TextRange(tab.Content.ContentStart, tab.Content.ContentEnd);
                    textRange.Load(reader.BaseStream, DataFormats.Text);
                }
                tab.IsDirty = false;
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                var err = string.Format(GetStringOrFallback("Msg_FileOpenError", CultureInfo.CurrentUICulture, "File open error: {0}"), ex.Message);
                MessageBox.Show(err, GetStringOrFallback("Title_Error", CultureInfo.CurrentUICulture, "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                _documentTabs.Remove(tab); 
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (tabControl.SelectedItem is DocumentTab currentTab && currentTab.Editor?.CanUndo == true)
            {
                currentTab.Editor.Undo();
            }
        }
        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (tabControl.SelectedItem is DocumentTab currentTab && currentTab.Editor?.CanRedo == true)
            {
                currentTab.Editor.Redo();
            }
        }

        private void FormatText(Action<TextSelection> formatAction)
        {
            if (tabControl.SelectedItem is DocumentTab currentTab && currentTab.Editor != null && !currentTab.Editor.Selection.IsEmpty)
            {
                formatAction(currentTab.Editor.Selection);
            }
        }

        private void BoldButton_Checked(object sender, RoutedEventArgs e) { FormatText(s => s.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold)); }
        private void BoldButton_Unchecked(object sender, RoutedEventArgs e) { FormatText(s => s.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal)); }
        private void ItalicButton_Checked(object sender, RoutedEventArgs e) { FormatText(s => s.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic)); }
        private void ItalicButton_Unchecked(object sender, RoutedEventArgs e) { FormatText(s => s.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal)); }
        private void UnderlineButton_Checked(object sender, RoutedEventArgs e) { FormatText(s => s.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline)); }
        private void UnderlineButton_Unchecked(object sender, RoutedEventArgs e) { FormatText(s => s.ApplyPropertyValue(Inline.TextDecorationsProperty, null)); }

        private void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fontComboBox.SelectedItem is ComboBoxItem item) FormatText(s => s.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(item.Content.ToString())));
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fontSizeComboBox.SelectedItem is ComboBoxItem item && double.TryParse(item.Content.ToString(), out double size))
                FormatText(s => s.ApplyPropertyValue(TextElement.FontSizeProperty, size));
        }

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            if (!(tabControl.SelectedItem is DocumentTab currentTab) || currentTab.Editor == null) return;
            var editor = currentTab.Editor;

            var prompt = GetStringOrFallback("Prompt_Find", CultureInfo.CurrentUICulture, "Find");
            var searchText = Microsoft.VisualBasic.Interaction.InputBox(prompt, prompt, "");
            if (string.IsNullOrWhiteSpace(searchText)) return;

            var textRange = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
            var index = textRange.Text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
            if (index == -1)
            {
                MessageBox.Show(GetStringOrFallback("Msg_NotFound", CultureInfo.CurrentUICulture, "Text not found."), GetStringOrFallback("Title_Info", CultureInfo.CurrentUICulture, "Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var start = editor.Document.ContentStart.GetPositionAtOffset(index + 1, LogicalDirection.Forward);
            if (start != null)
            {
                var end = start.GetPositionAtOffset(searchText.Length, LogicalDirection.Forward);
                if (end != null)
                {
                    editor.Selection.Select(start, end);
                    editor.Focus();
                }
            }
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            if (!(tabControl.SelectedItem is DocumentTab currentTab) || currentTab.Editor == null) return;
            var editor = currentTab.Editor;

            var culture = CultureInfo.CurrentUICulture;
            var promptOld = GetStringOrFallback("Prompt_Replace_Old", culture, "Text to replace");
            var oldText = Microsoft.VisualBasic.Interaction.InputBox(promptOld, promptOld, "");
            if (string.IsNullOrEmpty(oldText)) return;

            var promptNew = GetStringOrFallback("Prompt_Replace_New", culture, "New text");
            var newText = Microsoft.VisualBasic.Interaction.InputBox(promptNew, promptNew, "") ?? string.Empty;

            var current = editor.Document.ContentStart;
            while (current != null && current.CompareTo(editor.Document.ContentEnd) < 0)
            {
                var searchRange = new TextRange(current, editor.Document.ContentEnd);
                var result = searchRange.Text.IndexOf(oldText, StringComparison.OrdinalIgnoreCase);
                if (result < 0) break;

                var start = searchRange.Start.GetPositionAtOffset(result);
                if (start == null) break;

                var end = start.GetPositionAtOffset(oldText.Length);
                if (end == null) break;

                var rangeToReplace = new TextRange(start, end);
                rangeToReplace.Text = newText;

                current = rangeToReplace.End;
            }
        }

        private void Editor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is RichTextBox editor && editor.DataContext is DocumentTab tab)
            {
                tab.Editor = editor;
            }
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tabControl.SelectedItem is DocumentTab currentTab)
            {
                currentTab.IsDirty = true;
            }
            UpdateStatusBar();
        }

        private void ApplyLanguage(CultureInfo culture)
        {
            if (culture == null) culture = CultureInfo.CurrentUICulture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

       
            UpdateWindowTitle();
            UpdateUiForLanguage(culture);
            UpdateStatusBar();
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is DocumentTab tab)
            {
                if (tab.IsDirty)
                {
                    tabControl.SelectedItem = tab;
                    if (CheckUnsavedChanges()) return; 
                }
                _documentTabs.Remove(tab);

                if (_documentTabs.Count == 0)
                {
                    Application.Current.Shutdown();
                }
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                UpdateWindowTitle();
                UpdateStatusBar();
            }
        }

        private void UpdateUiForLanguage(CultureInfo culture)
        {
            btnNew.Content = GetStringOrFallback("Menu_New", culture, "New");
            btnOpen.Content = GetStringOrFallback("Menu_Open", culture, "Open");
            btnSave.Content = GetStringOrFallback("Menu_Save", culture, "Save");
            btnUndo.ToolTip = GetStringOrFallback("ToolTip_Undo", culture, "Undo");
            btnRedo.ToolTip = GetStringOrFallback("ToolTip_Redo", culture, "Redo");
            fontComboBox.ToolTip = GetStringOrFallback("ToolTip_FontFamily", culture, "Font");
            fontSizeComboBox.ToolTip = GetStringOrFallback("ToolTip_FontSize", culture, "Font Size");
            btnFind.Content = GetStringOrFallback("Button_Find", culture, "Find");
            btnReplace.Content = GetStringOrFallback("Button_Replace", culture, "Replace");
            btnTurkish.ToolTip = GetStringOrFallback("ToolTip_Turkish", culture, "Turkish");
            btnEnglish.ToolTip = GetStringOrFallback("ToolTip_English", culture, "English");
        }

        private string GetStringOrFallback(string key, CultureInfo culture, string fallback)
        {
            if (_rm == null) return fallback;
            try
            {
                var val = _rm.GetString(key, culture);
                return string.IsNullOrEmpty(val) ? fallback : val;
            }
            catch { return fallback; }
        }

        private void SwitchToEnglish_Click(object sender, RoutedEventArgs e) { ApplyLanguage(new CultureInfo("en")); }
        private void SwitchToTurkish_Click(object sender, RoutedEventArgs e) { ApplyLanguage(new CultureInfo("tr-TR")); }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            foreach (var tab in _documentTabs.ToList()) 
            {
                tabControl.SelectedItem = tab;
                if (CheckUnsavedChanges())
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        private void UpdateStatusBar()
        {
            if (wordCountLabel == null || charCountLabel == null)
                return;

            if (tabControl.SelectedItem is DocumentTab currentTab)
            {
                var text = new TextRange(currentTab.Content.ContentStart, currentTab.Content.ContentEnd).Text;
                var culture = CultureInfo.CurrentUICulture;

                var wordCount = text.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                var wordLabel = GetStringOrFallback("Status_Words", culture, "Words:");
                wordCountLabel.Text = $"{wordLabel} {wordCount}";

                var charCount = text.TrimEnd('\r', '\n').Length;
                var charLabel = GetStringOrFallback("Status_Chars", culture, "Characters:");
                charCountLabel.Text = $"{charLabel} {charCount}";
            }
            else
            {
                wordCountLabel.Text = string.Empty;
                charCountLabel.Text = string.Empty;
            }
        }
    }
}
