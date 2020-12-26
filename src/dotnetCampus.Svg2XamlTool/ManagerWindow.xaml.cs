using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace dotnetCampus.Svg2XamlTool
{
    /// <summary>
    /// ManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ManagerWindow : Window
    {
        protected static string AppPath;
        protected static string SettingFile;
        static ManagerWindow()
        {
            AppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SvgTool");
            SettingFile = Path.Combine(AppPath, "Setting.config");

            if (!Directory.Exists(AppPath))
            {
                Directory.CreateDirectory(AppPath);
            }
        }

        protected readonly List<ResourceDictionary> AllDictList = new List<ResourceDictionary>();
        protected readonly List<ResourceDictionary> UpdateDictList = new List<ResourceDictionary>();
        public ManagerWindow()
        {
            InitializeComponent();

            ImagePathTextBox.Text += @"D:\Projects\IIP-Win\EasiNote\Dependencies\EasiUI\Code\Cvte.EasiUI\Images";
            ImagePathTextBox.Text += Environment.NewLine;
            ImagePathTextBox.Text += @"D:\Projects\IIP-Win\EasiNote\Code\Core\EasiNote.Resources\Images";

            Loaded += ManagerWindow_Loaded;
        }

        private void ManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(SettingFile))
            {
                var setting = File.ReadAllText(SettingFile);
                GetDictList(setting);
            }
            else
            {
                InfoTextBlock.Text = "请点击设置按钮，输入项目矢量图路径";
            }
        }

        private void GetDictList(string setting)
        {
            var folders = setting.Split('\n', '\r');
            AllDictList.Clear();
            foreach (var folder in folders)
            {
                if (Directory.Exists(folder))
                {
                    var files = Directory.GetFiles(folder);
                    foreach (var file in files)
                    {
                        AllDictList.Add(new ResourceDictionary { Source = new Uri(file) });
                    }
                }
            }
        }

        private void SaveSettingButton_OnClick(object sender, RoutedEventArgs e)
        {
            var setting = ImagePathTextBox.Text.Trim();
            File.WriteAllText(SettingFile, setting);
            GetDictList(setting);
            SettingToggleButton.IsChecked = false;
        }

        private void MainGrid_OnDrop(object sender, DragEventArgs e)
        {
            var wpfSettings = new WpfDrawingSettings();
            wpfSettings.CultureInfo = wpfSettings.NeutralCultureInfo;
            var fileSvgReader = new FileSvgReader(wpfSettings);

            var tempSvgFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "Svg2XamlTemp.svg");

            try
            {
                var fileNames = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (fileNames == null || !fileNames.Any()) return;
                var svgs = fileNames.Where(x => Path.GetExtension(x) == ".svg").ToArray();

                if (!svgs.Any()) return;

                foreach (var svg in svgs)
                {
                    var content = File.ReadAllText(svg);
                    content = Regex.Replace(content, "<g id=\".*?\"", "<g");
                    File.WriteAllText(tempSvgFile, content);

                    var svgFileName = Path.GetFileNameWithoutExtension(svg);
                    if (svgFileName == null) continue;

                    Drawing drawing = fileSvgReader.GetDrawingGroup(tempSvgFile);
                    while (drawing is DrawingGroup && (((DrawingGroup)drawing).Children.Count == 1))
                    {
                        var dr = ((DrawingGroup)drawing).Children[0];
                        if (dr != null)
                        {
                            drawing = dr;
                        }
                        else
                        {
                            break;
                        }
                    }
                    var drawingImage = new DrawingImage(drawing);

                    var resourceDictionary = AllDictList.FirstOrDefault(x => x.Contains(svgFileName) && x.MergedDictionaries.Count < 1)
                        ?? AllDictList.FirstOrDefault(x => SvgMatchDict(x, svgFileName))
                        ?? AllDictList.OrderBy(x => x.Count).LastOrDefault();
                    if (resourceDictionary != null)
                    {
                        if (svgFileName.StartsWith("Image."))
                        {
                            resourceDictionary[svgFileName] = drawingImage;
                        }
                        else if (svgFileName.StartsWith("Geometry."))
                        {
                            var gd = drawingImage.Drawing as GeometryDrawing;
                            if (gd != null)
                            {
                                var geomtry = gd.Geometry;
                                resourceDictionary[svgFileName] = geomtry;
                            }
                            else
                            {
                                MessageBox.Show($"Geometry 类型的 SVG 文件 {svgFileName}.svg 不符合规范");
                            }
                        }

                        if (!UpdateDictList.Contains(resourceDictionary))
                        {
                            UpdateDictList.Add(resourceDictionary);
                        }
                    }
                }

                Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void Save()
        {
            foreach (var dict in UpdateDictList)
            {
                var xaml = GetXaml(dict);

                xaml = xaml.Replace(" xmlns:svg=\"http://sharpvectors.codeplex.com/runtime/\"", String.Empty);
                xaml = xaml.Replace(" Pen=\"{x:Null}\"", string.Empty);
                xaml = Regex.Replace(xaml, " svg:SvgLink.Key=\".*\"", String.Empty);
                xaml = Regex.Replace(xaml, " svg:SvgObject.Id=\".*\"", String.Empty);
                xaml = Regex.Replace(xaml, "<PathGeometry FillRule=\"EvenOdd\" Figures=\"([^T]*?)\" />", "<StreamGeometry>$1</StreamGeometry>");

                File.WriteAllText(dict.Source.AbsolutePath, xaml);
            }
        }

        private static string GetXaml(object obj)
        {
            var writerSettings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true,
                Encoding = Encoding.ASCII
            };

            string xaml;
            using (var xamlStream = new MemoryStream())
            {
                using (XmlWriter writer = XmlWriter.Create(xamlStream, writerSettings))
                {
                    XamlWriter.Save(obj, writer);
                }

                xaml = Encoding.ASCII.GetString(xamlStream.ToArray());
            }
            return xaml;
        }

        private static bool SvgMatchDict(ResourceDictionary dict, string svgName)
        {
            var fileName = Path.GetFileNameWithoutExtension(dict.Source.AbsolutePath);
            var keyName = fileName.Split('.').LastOrDefault();
            return !string.IsNullOrEmpty(keyName) && svgName.Contains(keyName);
        }

        private void ShowAllButton_OnClick(object sender, RoutedEventArgs e)
        {
            var iconWin=new IconView(AllDictList);
            iconWin.Show();
        }
    }
}
