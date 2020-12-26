using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace dotnetCampus.Svg2XamlTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var wpfSettings = new WpfDrawingSettings();
            wpfSettings.CultureInfo = wpfSettings.NeutralCultureInfo;
            _fileSvgReader = new FileSvgReader(wpfSettings);
        }

        private readonly FileSvgReader _fileSvgReader;

        /// <summary>
        /// 处理拖拽导入的SVG文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContainerGrid_OnDrop(object sender, DragEventArgs e)
        {
            try
            {
                var fileNames = e.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (fileNames == null || !fileNames.Any()) return;
                var svgs = fileNames.Where(x => Path.GetExtension(x) == ".svg").ToArray();

                if (!svgs.Any()) return;

                var sb = new StringBuilder();
                foreach (var svg in svgs)
                {
                    var svgFileName = Path.GetFileNameWithoutExtension(svg);
                    Drawing drawing = _fileSvgReader.GetDrawingGroup(svg);

                    //去掉冗余的层次
                    while (drawing is DrawingGroup drawingGroup && (drawingGroup.Children.Count == 1))
                    {
                        var dr = drawingGroup.Children[0];
                        if (dr != null)
                        {
                            if (drawingGroup.Transform != null)
                            {
                                if (dr is DrawingGroup @group)
                                {
                                    @group.Transform = drawingGroup.Transform;
                                }
                                else if (dr is GeometryDrawing geometryDrawing)
                                {
                                    geometryDrawing.Geometry.Transform = drawingGroup.Transform;
                                }
                            }

                            drawing = dr;
                        }
                        else
                        {
                            break;
                        }
                    }

                    var drawingImage = new DrawingImage(drawing);
                    if (drawing is GeometryDrawing geometryDrawing1)
                    {
                        if (geometryDrawing1.Geometry is PathGeometry geo)
                        {
                            var pathGeometry = new PathGeometry();
                            foreach (var figure in geo.Figures)
                            {
                                pathGeometry.Figures.Add(figure);
                            }

                            var combineGeometry = Geometry.Combine(Geometry.Empty, pathGeometry,
                                GeometryCombineMode.Union, geometryDrawing1.Geometry.Transform);
                            geometryDrawing1.Geometry = combineGeometry;
                            drawing = geometryDrawing1;
                        }
                    }

                    var xaml = GetXaml(drawingImage);

                    var bounds = drawing.Bounds;
                    var image = new Image
                    {
                        Source = drawingImage,
                        Width = bounds.Width,
                        Height = bounds.Height,
                        ToolTip = $"{drawing.Bounds.Width},{drawing.Bounds.Height}",
                        SnapsToDevicePixels = true,
                        UseLayoutRounding = true,
                        Stretch = Stretch.None,
                    };

                    IconsContainer.Children.Add(image);
                    HintText.Text = string.Empty;

                    sb.Append(xaml);
                    sb.Append(Environment.NewLine);
                    sb.Append(Environment.NewLine);
                    sb.Replace("<DrawingImage xmlns", $"<DrawingImage x:Key=\"{svgFileName}\" xmlns");
                }

                //替换掉不需要的字符串
                sb.Replace(" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"", string.Empty);
                sb.Replace(" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", string.Empty);
                sb.Replace(" xmlns:svg=\"http://sharpvectors.codeplex.com/runtime/\"", string.Empty);
                sb.Replace(" Pen=\"{x:Null}\"", string.Empty);


                var result = sb.ToString();
                result = Regex.Replace(result, " svg:SvgLink.Key=\".*\"", string.Empty);
                result = Regex.Replace(result, " svg:SvgObject.Id=\".*\"", string.Empty);
                result = Regex.Replace(result, "<PathGeometry FillRule=\"EvenOdd\" Figures=\"([^T]*?)\" />",
                    "<StreamGeometry>$1</StreamGeometry>");

                var tempXamlFile = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.xaml");

                WriteToFile(result, tempXamlFile);

                TryOpenXamlFile(tempXamlFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        /// <summary>
        /// 尝试打开 xaml 文件
        /// </summary>
        /// <param name="xamlFile"></param>
        private static void TryOpenXamlFile(string xamlFile)
        {
            try
            {
                Process.Start("notepad.exe", xamlFile);
                return;
            }
            catch (Win32Exception)
            {
                // 忽略
            }

            try
            {
                var processStartInfo = new ProcessStartInfo(xamlFile)
                {
                    UseShellExecute = true
                };
                Process.Start(processStartInfo);
                return;
            }
            catch (Exception)
            {
                // 忽略
            }
        }

        #region 辅助方法

        /// <summary>
        /// 将对象转换为XAML字符串并返回
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 将字符串写入指定文件中
        /// </summary>
        /// <param name="content"></param>
        /// <param name="filePath"></param>
        private static void WriteToFile(string content, string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create))
            using (var sw = new StreamWriter(fs, Encoding.ASCII))
            {
                sw.Write(content);
                sw.Flush();
            }
        }

        #endregion
    }
}