using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace dosya_duzenleme
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _filePath = string.Empty;
        private bool _isTextChanged = false;
        public MainWindow()
        {
            InitializeComponent();
            UpdateWindowTitle();
        }
        private void UpdateWindowTitle() =>
            Title = string.IsNullOrEmpty(_filePath)
                ? "Metin Düzenleyici - Yeni Belge"
                : $"Metin Düzenleyici - {Path.GetFileName(_filePath)}";

        private bool CheckUnsavedChanges()
        {
            if (!_isTextChanged) return false;

            var result = MessageBox.Show("Kaydedilmemiş değişiklikler var. Kaydetmek istiyor musunuz?",
                "Uyarı", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

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

        private void new_Click(object sender, RoutedEventArgs e)
        {
            if (CheckUnsavedChanges()) return;

            _filePath = string.Empty;
            yazi1.Document.Blocks.Clear();
            _isTextChanged = false;
            UpdateWindowTitle();
        }

        private void ac_Click(object sender, RoutedEventArgs e)
        {
            if (CheckUnsavedChanges()) return;

            var openDialog = new OpenFileDialog();
            if (openDialog.ShowDialog() == true)
            {
                _filePath = openDialog.FileName;
                LoadFileContent();
            }
        }
        private void kaydet_Click(object sender, RoutedEventArgs e) => SaveFile(false);

        private void SaveFile(bool saveAs)
        {
            if (saveAs || string.IsNullOrEmpty(_filePath))
            {
                var saveDialog = new SaveFileDialog();
                if (saveDialog.ShowDialog() != true) return;
                _filePath = saveDialog.FileName;
            }

            using (var writer = new StreamWriter(_filePath))
            {
                new TextRange(yazi1.Document.ContentStart, yazi1.Document.ContentEnd)
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
                    yazi1.Document.Blocks.Clear();
                    new TextRange(yazi1.Document.ContentStart, yazi1.Document.ContentEnd)
                        .Load(reader.BaseStream, DataFormats.Text);
                }
                _isTextChanged = false;
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya açma hatası: {ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void geriAl_Click(object sender, RoutedEventArgs e)
        {
            if (yazi1.CanUndo) yazi1.Undo();
        }

        private void yinele_Click(object sender, RoutedEventArgs e)
        {
            if (yazi1.CanRedo) yazi1.Redo();
        }
        private void FormatText(Action<TextSelection> formatAction)
        {
            if (!yazi1.Selection.IsEmpty) formatAction(yazi1.Selection);
        }
        
        private void BoldButton_Click(object sender, RoutedEventArgs e) =>
          FormatText(s => s.ApplyPropertyValue(
              TextElement.FontWeightProperty,
              s.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight weight && weight == FontWeights.Bold
                  ? FontWeights.Normal
                  : FontWeights.Bold));

        private void ItalicButton_Click(object sender, RoutedEventArgs e) =>
            FormatText(s => s.ApplyPropertyValue(
                TextElement.FontStyleProperty,
                s.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle style && style == FontStyles.Italic
                    ? FontStyles.Normal
                    : FontStyles.Italic));

        private void UnderlineButton_Click(object sender, RoutedEventArgs e) =>
            FormatText(s => s.ApplyPropertyValue(
                Inline.TextDecorationsProperty,
                s.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection decorations &&
                decorations == TextDecorations.Underline
                    ? null
                    : TextDecorations.Underline));

        private void fontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fontComboBox.SelectedItem is ComboBoxItem item)
                FormatText(s => s.ApplyPropertyValue(
                    TextElement.FontFamilyProperty,
                    new FontFamily(item.Content.ToString())));
        }

        private void fontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fontSizeComboBox.SelectedItem is ComboBoxItem item &&
                double.TryParse(item.Content.ToString(), out double size))
                FormatText(s => s.ApplyPropertyValue(TextElement.FontSizeProperty, size));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var searchText = ShowInputDialog("Bul");
            if (string.IsNullOrEmpty(searchText)) return;

            var textRange = new TextRange(yazi1.Document.ContentStart, yazi1.Document.ContentEnd);
            var index = textRange.Text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);

            if (index == -1)
            {
                MessageBox.Show("Metin bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var start = yazi1.Document.ContentStart.GetPositionAtOffset(index);
            var end = start?.GetPositionAtOffset(searchText.Length);

            if (start != null && end != null)
            {
                yazi1.Selection.Select(start, end);
                yazi1.ScrollToVerticalOffset(yazi1.Selection.Start.GetCharacterRect(LogicalDirection.Forward).Top);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var searchText = ShowInputDialog("Değiştirilecek metin");
            if (string.IsNullOrEmpty(searchText)) return;

            var replaceText = ShowInputDialog("Yeni metin");
            var textRange = new TextRange(yazi1.Document.ContentStart, yazi1.Document.ContentEnd);
            var newText = textRange.Text.Replace(searchText, replaceText);

            yazi1.Document.Blocks.Clear();
            yazi1.Document.Blocks.Add(new Paragraph(new Run(newText)));
            _isTextChanged = true;
        }
        private string ShowInputDialog(string title) =>
           Microsoft.VisualBasic.Interaction.InputBox($"{title} metnini girin:", title, "");

        private void yazi1_TextChanged(object sender, TextChangedEventArgs e) => _isTextChanged = true;

        private void boldButton_Checked(object sender, RoutedEventArgs e)
        {
            FormatText(selection =>
       selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold));
        }
        private void BoldButton_Unchecked(object sender, RoutedEventArgs e)
        {
            FormatText(selection =>
                selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal));
        }

        private void italicButton_Checked(object sender, RoutedEventArgs e)
        {
            if (italicButton.IsChecked == true)
            {
                FormatText(s => s.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic));
            }
            else
            {
                FormatText(s => s.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal));
            }
        }

        private void underlineButton_Checked(object sender, RoutedEventArgs e)
        {
            FormatText(selection =>
      selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline));
        }
        private void UnderlineButton_Unchecked(object sender, RoutedEventArgs e)
        {
            FormatText(selection =>
                selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null));
        }
    }
}