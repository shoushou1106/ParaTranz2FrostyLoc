using FrostySdk.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.International.Converters.TraditionalChineseToSimplifiedConverter;
using Microsoft.Win32;
using ParaTranz2FrostyLoc.Logger;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static FrostySdk.GeometryDeclarationDesc;

namespace ParaTranz2FrostyLoc
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            App.Logger.AddBinding(LogTextBox, TextBox.TextProperty);
        }

        private void HistogramChunkImport_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Title = "Import Histogram Chunk",
                Filter = "Chunk (*.chunk)|*.chunk|Binary (*.bin)|*.bin|Any Files (*.*)|*.*"
            };
            if (ofd.ShowDialog() is true)
            {
                App.Logger.LogInformation(ofd.FileName);
                App.Database.InitializeHistograms(ofd.FileName);
                App.Logger.LogInformation("Histogram Chunk imported");
            }
            GC.Collect();
        }

        private void StringBinaryChunkImport_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Title = "Import String Binary Chunk",
                Filter = "Chunk (*.chunk)|*.chunk|Binary (*.bin)|*.bin|Any Files (*.*)|*.*"
            };
            if (ofd.ShowDialog() is true)
            {
                App.Logger.LogInformation(ofd.FileName);
                App.Database.InitializeStrings(ofd.FileName);
                App.Logger.LogInformation("String Binary Chunk imported");
            }
            GC.Collect();
        }

        Dictionary<uint, string> newStrings = new();

        private async void ParatranzJsonImport_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Title = "Import ParaTranz Json",
                Filter = "Json (*.json)|*.json|Any Files (*.*)|*.*"
            };
            if (ofd.ShowDialog() is true)
            {
                App.Logger.LogInformation(ofd.FileName);

                //ParaTranzStrings = JsonSerializer.Deserialize<List<ParaTranzJson>>(reader.ReadToEnd()) ?? new();
                
                JsonDocument jsonDocument = JsonDocument.Parse("{\"header\": " + File.ReadAllText(ofd.FileName, System.Text.Encoding.UTF8) + "}");
                JsonElement jsonElement = jsonDocument.RootElement.GetProperty("header");

                App.Logger.LogInformation("ParaTranz Json readed");

                int ignored = 0;
                int modified = 0;
                int added = 0;

                foreach (JsonElement element in jsonElement.EnumerateArray())
                {
                    uint hash = uint.Parse(element.GetProperty("key").ToString(), System.Globalization.NumberStyles.HexNumber);
                    string str = element.GetProperty("translation").ToString();

                    if (string.IsNullOrEmpty(str))
                    {
                        ignored++;
                    }
                    else if (App.Database.stringChunk.strings.ContainsKey(hash) && str != App.Database.stringChunk.strings[hash])
                    {
                        if (newStrings.ContainsKey(hash))
                            newStrings[hash] = str;
                        else
                            newStrings.Add(hash, str);

                        modified++;
                    }
                    else if (str == App.Database.stringChunk.strings[hash])
                    {
                        ignored++;
                    }
                    else
                    {
                        if (newStrings.ContainsKey(hash))
                            newStrings[hash] = str;
                        else
                            newStrings.Add(hash, str);

                        added++;
                    }

                }

                App.Logger.LogInformation($"{modified} strings modified and {added} strings added and {ignored} strings ignored");
                App.Logger.LogInformation("ParaTranz Json imported");
            }
            GC.Collect();
        }

        private void CHT2CHS_Click(object sender, RoutedEventArgs e)
        {
            int i = 0;
            foreach (var key in App.Database.stringChunk.strings.Keys)
            {
                if (newStrings.ContainsKey(key))
                    newStrings[key] = ChineseConverter.Convert(App.Database.stringChunk.strings[key], ChineseConversionDirection.TraditionalToSimplified);
                else
                    newStrings.Add(key, ChineseConverter.Convert(App.Database.stringChunk.strings[key], ChineseConversionDirection.TraditionalToSimplified));
                i++;
            }
            App.Logger.LogInformation(i + " strings converted");
            App.Logger.LogInformation("CHT2CHS Complete");
            GC.Collect();
        }

        private void ExportChunk_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfdString = new SaveFileDialog()
            {
                Title = "Save String Binary Chunk",
                Filter = "Chunk (*.chunk)|*.chunk|Binary (*.bin)|*.bin|Any Files (*.*)|*.*",
            };
            SaveFileDialog sfdHistogram = new SaveFileDialog()
            {
                Title = "Save Histogram Chunk",
                Filter = "Chunk (*.chunk)|*.chunk|Binary (*.bin)|*.bin|Any Files (*.*)|*.*"
            };
            if (sfdString.ShowDialog() is true && sfdHistogram.ShowDialog() is true)
            {
                App.Logger.LogInformation("New String Binary Chunk Path: " + sfdString.FileName);
                App.Logger.LogInformation("Read Histogram Chunk Path: " + sfdHistogram.FileName);

                App.Database.WriteChunks(sfdHistogram.FileName, sfdString.FileName, App.Logger, Enumerable.Range(0, TextBoxTest1.Text.Length)
                     .Where(x => x % 2 == 0)
                     .Select(x => Convert.ToByte(TextBoxTest1.Text.Substring(x, 2), 16))
                     .ToArray(),
                     newStrings);
                App.Logger.LogInformation("Writing Complete");
            }
        }

        private void ShowUnsupportChars_Click(object sender, RoutedEventArgs e)
        {
            TextBoxTest1.Text = App.Database.ShowUnsupportChars(newStrings.Values.ToList());
        }

        #region - Debug -

        private void Test1_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            foreach (var str in newStrings)
            {
                sb.Append("0x");
                sb.Append(str.Key.ToString("X"));
                sb.Append(": \"");
                sb.Append(str.Value.Replace("\n", "\\r\\n").Replace("\"", "\\\""));
                sb.Append("\", ");
            }
            sb.Append("}");

            Clipboard.SetText(sb.ToString());
        }

        private void Test2_Click(object sender, RoutedEventArgs e)
        {
            App.Database.histogramChunk.values.Clear();
        }

        private void Test3_Click(object sender, RoutedEventArgs e)
        {
            ushort tmp = App.Database.histogramChunk.values[0x94];
            byte b = 0xB7 - 0x80;
            char dstStr = (char)App.Database.histogramChunk.values[(b + (tmp << 7))];

            App.Logger.LogDebug(dstStr.ToString());

        }

        #endregion
    }
}