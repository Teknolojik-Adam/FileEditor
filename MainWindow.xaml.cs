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

namespace dosya_duzenleme
{
    public partial class MainWindow : Window
    {
        private ResourceManager? _rm;
        private string _filePath = string.Empty;
        private bool _isTextChanged = false;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                _rm = new ResourceManager("dosya_duzenleme.Resources", typeof(MainWindow).Assembly);
            }
            catch
            {
                _rm = null;
            }

            // Varsayılan olarak İngilizce başlatmak için burayı değiştirin:
            ApplyLanguage(new CultureInfo("en"));

            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            var culture = CultureInfo.CurrentUICulture;
            var titleNew = GetStringOrFallback("Title_NewDocument", culture, "Text Editor - New Document");
            var titleEditor = GetStringOrFallback("Title_Editor", culture, "Text Editor");
            Title = string.IsNullOrEmpty(_filePath) ? titleNew : $"{titleEditor} - {Path.GetFileName(_filePath)}";
        }

        private bool CheckUnsavedChanges()
        {
            if (!_isTextChanged) return false;
            var culture = CultureInfo.CurrentUICulture;
            var message = GetStringOrFallback("Msg_UnsavedChanges", culture, "There are unsaved changes. Do you want to save them?");
            var caption = GetStringOrFallback("Title_Warning", culture, "Warning");
            var result = MessageBox.Show(message, caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            return HandleSaveDialogResult(result);
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
            if (CheckUnsavedChanges()) return;
            _filePath = string.Empty;
            editor.Document.Blocks.Clear();
            _isTextChanged = false;
            UpdateWindowTitle();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (CheckUnsavedChanges()) return;
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
            {
                _filePath = dlg.FileName;
                LoadFileContent();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => SaveFile(false);

        private void SaveFile(bool saveAs)
        {
            if (saveAs || string.IsNullOrEmpty(_filePath))
            {
                var dlg = new SaveFileDialog();
                if (dlg.ShowDialog() != true) return;
                _filePath = dlg.FileName;
            }

            using (var writer = new StreamWriter(_filePath))
            {
                new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd)
                    .Save(writer.BaseStream, DataFormats.Text);
            }

            _isTextChanged = false;
            UpdateWindowTitle();
        }

        private void LoadFileContent()
        {
            try
            {
                using (var reader = new StreamReader(_filePath))
                {
                    editor.Document.Blocks.Clear();
                    new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd)
                        .Load(reader.BaseStream, DataFormats.Text);
                }
                _isTextChanged = false;
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                var err = string.Format(GetStringOrFallback("Msg_FileOpenError", CultureInfo.CurrentUICulture, "File open error: {0}"), ex.Message);
                MessageBox.Show(err, GetStringOrFallback("Title_Error", CultureInfo.CurrentUICulture, "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e) { if (editor.CanUndo) editor.Undo(); }
        private void Redo_Click(object sender, RoutedEventArgs e) { if (editor.CanRedo) editor.Redo(); }

        private void FormatText(Action<TextSelection> formatAction) { if (!editor.Selection.IsEmpty) formatAction(editor.Selection); }

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

            var start = editor.Document.ContentStart.GetPositionAtOffset(index, LogicalDirection.Forward);
            var end = start?.GetPositionAtOffset(searchText.Length, LogicalDirection.Forward);
            if (start != null && end != null)
            {
                editor.Selection.Select(start, end);
                editor.ScrollToVerticalOffset(editor.Selection.Start.GetCharacterRect(LogicalDirection.Forward).Top);
            }
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            var promptOld = GetStringOrFallback("Prompt_Replace_Old", CultureInfo.CurrentUICulture, "Text to replace");
            var oldText = Microsoft.VisualBasic.Interaction.InputBox(promptOld, promptOld, "");
            if (string.IsNullOrEmpty(oldText)) return;
            var promptNew = GetStringOrFallback("Prompt_Replace_New", CultureInfo.CurrentUICulture, "New text");
            var newText = Microsoft.VisualBasic.Interaction.InputBox(promptNew, promptNew, "") ?? string.Empty;

            var textRange = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
            var replaced = textRange.Text.Replace(oldText, newText);
            editor.Document.Blocks.Clear();
            editor.Document.Blocks.Add(new Paragraph(new Run(replaced)));
            _isTextChanged = true;
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e) { _isTextChanged = true; }

        private void ApplyLanguage(CultureInfo culture)
        {
            if (culture == null) culture = CultureInfo.CurrentUICulture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // Window title
            UpdateWindowTitle();

            // Buttons & tooltips
            btnNew.Content = GetStringOrFallback("Menu_New", culture, culture.TwoLetterISOLanguageName == "en" ? "New" : "Yeni");
            btnOpen.Content = GetStringOrFallback("Menu_Open", culture, culture.TwoLetterISOLanguageName == "en" ? "Open" : "Aç");
            btnSave.Content = GetStringOrFallback("Menu_Save", culture, culture.TwoLetterISOLanguageName == "en" ? "Save" : "Kaydet");

            btnUndo.ToolTip = GetStringOrFallback("ToolTip_Undo", culture, culture.TwoLetterISOLanguageName == "en" ? "Undo" : "Geri Al");
            btnRedo.ToolTip = GetStringOrFallback("ToolTip_Redo", culture, culture.TwoLetterISOLanguageName == "en" ? "Redo" : "Yinele");

            fontComboBox.ToolTip = GetStringOrFallback("ToolTip_FontFamily", culture, culture.TwoLetterISOLanguageName == "en" ? "Font" : "Yazı Tipi");
            fontSizeComboBox.ToolTip = GetStringOrFallback("ToolTip_FontSize", culture, culture.TwoLetterISOLanguageName == "en" ? "Font Size" : "Yazı Boyutu");

            btnFind.Content = GetStringOrFallback("Button_Find", culture, culture.TwoLetterISOLanguageName == "en" ? "Find" : "Bul");
            btnReplace.Content = GetStringOrFallback("Button_Replace", culture, culture.TwoLetterISOLanguageName == "en" ? "Replace" : "Değiştir");

            btnTurkish.ToolTip = GetStringOrFallback("ToolTip_Turkish", culture, "Turkish");
            btnEnglish.ToolTip = GetStringOrFallback("ToolTip_English", culture, "English");

            // Placeholder text update if doc empty or default
            var placeholder = GetStringOrFallback("Placeholder_Text", culture, culture.TwoLetterISOLanguageName == "en" ? "Type your text here..." : "Metninizi buraya yazın...");
            var docText = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
            if (string.IsNullOrWhiteSpace(docText) || docText.Trim() == "Metninizi buraya yazın..." || docText.Trim() == "Type your text here...")
            {
                editor.Document.Blocks.Clear();
                editor.Document.Blocks.Add(new Paragraph(new Run(placeholder)));
            }
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
    }
}
