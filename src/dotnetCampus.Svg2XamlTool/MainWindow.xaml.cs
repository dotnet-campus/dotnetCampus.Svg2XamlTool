using System;
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
        private readonly FileSvgReader _fileSvgReader;
        private readonly string _tempXamlFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "Svg2XamlTemp.xaml");

        public MainWindow()
        {
            InitializeComponent();

            var wpfSettings = new WpfDrawingSettings();
            wpfSettings.CultureInfo = wpfSettings.NeutralCultureInfo;
            _fileSvgReader = new FileSvgReader(wpfSettings);
        }

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
                    while (drawing is DrawingGroup && (((DrawingGroup)drawing).Children.Count == 1))
                    {
                        var dgroup = (DrawingGroup)drawing;
                        var dr = dgroup.Children[0];
                        if (dr != null)
                        {
                            if (dgroup.Transform != null)
                            {
                                if (dr is DrawingGroup)
                                {
                                    ((DrawingGroup)dr).Transform = dgroup.Transform;
                                }
                                else if (dr is GeometryDrawing)
                                {
                                    ((GeometryDrawing)dr).Geometry.Transform = dgroup.Transform;
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
                    if (drawing is GeometryDrawing)
                    {
                        var geometryDraing = (GeometryDrawing)drawing;
                        var geo = geometryDraing.Geometry as PathGeometry;
                        if (geo != null)
                        {
                            var pathGeometry = new PathGeometry();
                            foreach (var figure in geo.Figures)
                            {
                                pathGeometry.Figures.Add(figure);
                            }
                            var combineGeometry = Geometry.Combine(Geometry.Empty, pathGeometry, GeometryCombineMode.Union, geometryDraing.Geometry.Transform);
                            geometryDraing.Geometry = combineGeometry;
                            drawing = geometryDraing;
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
                    HintText.Text = String.Empty;

                    sb.Append(xaml);
                    sb.Append(Environment.NewLine);
                    sb.Append(Environment.NewLine);
                    sb.Replace("<DrawingImage xmlns", string.Format("<DrawingImage x:Key=\"{0}\" xmlns", svgFileName));
                }

                //替换掉不需要的字符串
                sb.Replace(" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"", String.Empty);
                sb.Replace(" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", String.Empty);
                sb.Replace(" xmlns:svg=\"http://sharpvectors.codeplex.com/runtime/\"", String.Empty);
                sb.Replace(" Pen=\"{x:Null}\"", string.Empty);


                var result = sb.ToString();
                result = Regex.Replace(result, " svg:SvgLink.Key=\".*\"", String.Empty);
                result = Regex.Replace(result, " svg:SvgObject.Id=\".*\"", String.Empty);
                result = Regex.Replace(result, "<PathGeometry FillRule=\"EvenOdd\" Figures=\"([^T]*?)\" />", "<StreamGeometry>$1</StreamGeometry>");

                WriteToFile(result, _tempXamlFile);
                //将结果保存到临时文件夹中，并用记事本打开
                Process.Start("notepad.exe", _tempXamlFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
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
