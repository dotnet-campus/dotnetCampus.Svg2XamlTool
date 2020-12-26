using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace dotnetCampus.Svg2XamlTool
{
    /// <summary>
    /// Geometry显示编辑界面
    /// </summary>
    public partial class GeometryToolView : UserControl
    {
        public string StandardPrefix = "MCQL";

        public string MiniLang
        {
            get { return GeometryTextBox.Text.Trim(); }
            set { GeometryTextBox.Text = value; }
        }

        public GeometryToolView()
        {
            InitializeComponent();
        }

        private void BtnShow_OnClick(object sender, RoutedEventArgs e)
        {
            MiniLang = FormattedMiniLanguage(MiniLang);
            ShowInPath(MiniLang);
        }

        private void ShowInPath(string miniLang)
        {
            try
            {
                var geometry = Geometry.Parse(miniLang);
                ShowerPath.Data = geometry;
                var bounds = geometry.Bounds;
                InfoTextBlock.Text = $"起点 {(int)bounds.Left},{(int)bounds.Top}  宽高 {(int)bounds.Width},{(int)bounds.Height}";
            }
            catch (Exception)
            {
                //UserNotify.Show("Data数据格式不对");
            }
        }

        #region 格式化MiniLanguage

        /// <summary>
        /// 获取格式化MiniLanguage
        /// </summary>
        /// <param name="miniLang"></param>
        /// <exception cref="FormatException">包含规定字符以外的非法字符</exception>
        private string FormattedMiniLanguage(string miniLang)
        {
            miniLang = miniLang.ToUpper(CultureInfo.InvariantCulture);

            if (Regex.IsMatch(miniLang, "[^\\d-FMHVLACQSTZE., ]"))
            {
                //UserNotify.Show("好像无法转换为Geometry");
                return miniLang;
            }

            //换行转为空格
            miniLang = Regex.Replace(miniLang, "(\r\n|\r)", " ");

            miniLang = Regex.Replace(miniLang, "([HVLACQST])", " $1");

            //保存字符Z前后都有空格
            miniLang = Regex.Replace(miniLang, @"(\d)(Z)|(Z)(\d)", "$1 $2");
            //多个连续点用逗号分隔情况改为用空格分隔
            miniLang = Regex.Replace(miniLang, @"([\d\.]+,[\d\.]+),", "$1 ");
            //将多个空格转为一个空格
            miniLang = Regex.Replace(miniLang, " {2,}", " ");
            //去除逗号前的空格
            miniLang = Regex.Replace(miniLang, " ,", ",");
            //去除特殊字母和逗号后的空格
            miniLang = Regex.Replace(miniLang, @"([FMHVLACQST,]) +(\d)", "$1$2");
            return miniLang;
        }

        #endregion

        #region 平移变化

        private string Translate(string miniLang, double xChange, double yChange)
        {
            var items = miniLang.Split(' ');

            var sb = new StringBuilder();
            foreach (var item in items)
            {
                string tempItem;

                var prefix = item[0].ToString(CultureInfo.InvariantCulture);
                if (StandardPrefix.Contains(prefix))
                {
                    tempItem = item[0] + TranslateXandY(item.Substring(1), xChange, yChange);
                }
                else if (prefix == "H" || prefix == "V")
                {
                    tempItem = TranslateXorY(item, xChange, yChange);
                }
                else if (item.Contains(",") && !item.StartsWith("A"))
                {
                    tempItem = TranslateXandY(item, xChange, yChange);
                }
                else
                {
                    tempItem = item;
                }
                sb.Append(tempItem).Append(" ");
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// 需要同时转换X和Y
        /// </summary>
        /// <param name="item"></param>
        /// <param name="xChange"></param>
        /// <param name="yChange"></param>
        /// <returns></returns>
        private static string TranslateXandY(string item, double xChange, double yChange)
        {
            var num = item.Split(',');
            var sb = new StringBuilder();
            int index = 0;
            while (index < num.Length)
            {
                var x = Convert.ToDouble(num[index]);
                var y = Convert.ToDouble(num[index + 1]);
                index += 2;
                sb.Append($"{x + xChange},{y + yChange} ");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 只转换X或Y
        /// </summary>
        /// <param name="item"></param>
        /// <param name="xChange"></param>
        /// <param name="yChange"></param>
        /// <returns></returns>
        private static string TranslateXorY(string item, double xChange, double yChange)
        {
            var tempItem = String.Empty;
            if (item.StartsWith("H"))
            {
                var x = Convert.ToDouble(item.Substring(1));
                tempItem = "H" + (x + xChange);
            }
            else if (item.StartsWith("V"))
            {
                var y = Convert.ToDouble(item.Substring(1));
                tempItem = "V" + (y + yChange);
            }
            return tempItem;
        }

        #endregion

        #region 旋转变化

        /// <summary>
        /// 旋转操作。发送旋转的话，原来的H3，V4这样的就不能用了
        /// </summary>
        /// <param name="miniLang"></param>
        /// <param name="angle"></param>
        /// <param name="noUse">暂时没用的参数</param>
        /// <returns></returns>
        private string Rotate(string miniLang, double angle, double noUse)
        {
            var center = GetGeometryCenter(miniLang);

            var items = ConvertHVToL(miniLang.Split(' '));
            var count = items.Length;
            for (int i = 0; i < count; i++)
            {
                if (items[i].Contains(',') && !items[i].StartsWith("A"))
                {
                    int startIndex = 0;
                    if (StandardPrefix.Contains(items[i][0]))
                    {
                        startIndex = 1;
                    }
                    var commaIndex = items[i].IndexOf(',');
                    double x = Convert.ToDouble(items[i].Substring(startIndex, commaIndex - startIndex));
                    var ystring = items[i].Substring(commaIndex + 1);
                    double y = Convert.ToDouble(ystring);

                    var point = RotateAroundPoint(new Point(x, y), center, angle);

                    if (0 == startIndex)
                    {
                        items[i] = $"{point.X},{point.Y}";
                    }
                    else if (1 == startIndex)
                    {
                        items[i] = $"{items[i][0]}{point.X},{point.Y}";
                    }
                }
                else if (i > 0 && items[i - 1].StartsWith("A"))
                {
                    var sourceAngle = Convert.ToDouble(items[i]);
                    items[i] = (sourceAngle + angle).ToString(CultureInfo.InvariantCulture);
                }
            }

            var sb = new StringBuilder();
            foreach (var item in items)
            {
                sb.Append(item).Append(" ");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 获取一个点绕另一个点旋转后得到的新点
        /// </summary>
        /// <param name="sourcePoint"></param>
        /// <param name="aroundPoint"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        private Point RotateAroundPoint(Point sourcePoint, Point aroundPoint, double angle)
        {
            var sourceVector = sourcePoint - aroundPoint;
            var d = angle * Math.PI / 180;
            var x = sourceVector.X;
            var y = sourceVector.Y;
            var v = new Vector(x * Math.Cos(d) - y * Math.Sin(d), x * Math.Sin(d) + y * Math.Cos(d));
            return aroundPoint + v;
        }


        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// 将 H V 转为 L
        /// </summary>
        /// <param name="miniLangArray">MiniLang根据空格拆分的字符串组</param>
        /// <returns>转换后的MiniLang根据空格拆分的字符串组</returns>
        private string[] ConvertHVToL(string[] miniLangArray)
        {
            var items = miniLangArray;

            var count = items.Length;
            for (int i = 0; i < count; i++)
            {
                if (items[i].StartsWith("V"))
                {
                    string x = String.Empty;
                    string y = items[i].Substring(1);
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (items[j].Contains(",") && !items[j].StartsWith("A"))
                        {
                            var startIndex = 0;
                            if (StandardPrefix.Contains(items[j][0]))
                            {
                                startIndex = 1;
                            }
                            var commaIndex = items[j].IndexOf(',');
                            x = items[j].Substring(startIndex, commaIndex - startIndex);
                            break;
                        }
                        if (items[j].StartsWith("H"))
                        {
                            x = items[j].Substring(1);
                            break;
                        }
                    }
                    items[i] = $"L{x},{y}";
                }
                if (items[i].StartsWith("H"))
                {
                    string x = items[i].Substring(1);
                    string y = String.Empty;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (items[j].Contains(",") && !items[j].StartsWith("A"))
                        {
                            var commaIndex = items[j].IndexOf(',');
                            y = items[j].Substring(commaIndex + 1);
                            break;
                        }
                        if (items[j].StartsWith("V"))
                        {
                            y = items[j].Substring(1);
                            break;
                        }
                    }
                    items[i] = $"L{x},{y}";
                }
            }
            return items;
        }

        #endregion

        #region 转换操作

        private void Translate(double x, double y)
        {
            Transform(x, y, Translate);
        }

        private void Rotate(double angle)
        {
            Transform(angle, 0, Rotate);
        }

        private void Transform(double x, double y, Func<string, double, double, string> func)
        {
            if (String.IsNullOrWhiteSpace(MiniLang)) return;
            MiniLang = func(MiniLang, x, y);
            MiniLang = FormattedMiniLanguage(MiniLang);
            ShowInPath(MiniLang);
        }

        #endregion

        #region 保留一位

        private void ShortDoubleButton_OnClick(object sender, RoutedEventArgs e)
        {
            MiniLang = Regex.Replace(MiniLang, "(\\d+)\\.(\\d)\\d+", "$1.$2");
        }

        #endregion

        #region 翻转

        private void HorizontalFlipButton_OnClick(object sender, RoutedEventArgs e)
        {
            Flip();
        }

        private void VerticalFlipButton_OnClick(object sender, RoutedEventArgs e)
        {
            Flip(false);
        }

        private void Flip(bool isX = true)
        {
            var center = GetGeometryCenter(MiniLang);

            MiniLang = FormattedMiniLanguage(MiniLang);
            var items = ConvertHVToL(MiniLang.Split(' '));

            var miniItemList = new List<object>();
            foreach (var item in items)
            {
                if (Regex.IsMatch(item, "([MCQL]\\d+,\\d+)|(\\d+,\\d+)") && !item.StartsWith("A"))
                {
                    var miniLangItem = new MiniLangItem(item);
                    if (isX)
                    {
                        miniLangItem.X = center.X * 2 - miniLangItem.X;
                    }
                    else
                    {
                        miniLangItem.Y = center.Y * 2 - miniLangItem.Y;
                    }
                    miniItemList.Add(miniLangItem);
                }
                else
                {
                    miniItemList.Add(item);
                }
            }

            MiniLang = String.Join(" ", miniItemList);

            if (Regex.IsMatch(MiniLang, @"(A\d+,\d+ \d \d) 0"))
            {
                MiniLang = Regex.Replace(MiniLang, @"(A\d+,\d+ \d \d) 0", "$1 1");
            }
            else
            {
                MiniLang = Regex.Replace(MiniLang, @"(A\d+,\d+ \d \d) 1", "$1 0");
            }

            ShowInPath(MiniLang);
        }

        #endregion

        /// <summary>
        /// 根据miniLang获取对应Geometry描绘图形的中心点
        /// </summary>
        /// <param name="miniLang"></param>
        /// <returns></returns>
        private static Point GetGeometryCenter(string miniLang)
        {
            var geometry = Geometry.Parse(miniLang);
            var rect = geometry.Bounds;
            var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            return center;
        }

        private void XTranslateButton_OnClick(object sender, RoutedEventArgs e)
        {
            var text = XTranslateTextBox.Text.Trim();
            double d;
            if (Double.TryParse(text, out d))
            {
                Translate(d, 0);
            }
        }

        private void YTranslateButton_OnClick(object sender, RoutedEventArgs e)
        {
            var text = YTranslateTextBox.Text.Trim();
            double d;
            if (Double.TryParse(text, out d))
            {
                Translate(0, d);
            }
        }

        private void RotateButton_OnClick(object sender, RoutedEventArgs e)
        {
            var rotateText = RotateTextBox.Text.Trim();
            double rotate;
            if (Double.TryParse(rotateText, out rotate))
            {
                Rotate(rotate);
            }
        }

        private void AddGeometryButton_OnClick(object sender, RoutedEventArgs e)
        {
            ContainerCanvas.Children.Add(new Path { Data = ShowerPath.Data, Stroke = Brushes.Teal, StrokeThickness = 1 });
            ShowerPath.Data = null;
            GeometryTextBox.Text = String.Empty;
        }

        private void ContainerCanvas_OnMouseMove(object sender, MouseEventArgs e)
        {
            var point = e.GetPosition(ContainerCanvas);
            PointTextBlock.Text = point.ToString();
        }

        private void ContainerCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var point = e.GetPosition(ContainerCanvas);
            GeometryTextBox.Text += $" {point}";
        }

        private void ClearButton_OnClick(object sender, RoutedEventArgs e)
        {
            var paths = ContainerCanvas.Children.OfType<Path>().Where(x => !Equals(x, ShowerPath)).ToList();
            foreach (Path path in paths)
            {
                ContainerCanvas.Children.Remove(path);
            }

            ShowerPath.Data = null;
            GeometryTextBox.Text = string.Empty;
        }
    }

    class MiniLangItem
    {
        public string Action { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public MiniLangItem(string item)
        {
            if (item == "Z")
            {
                Action = "Z";
                return;
            }
            string numberString = item;
            if ("MLAQC".Contains(item[0]))
            {
                Action = item[0].ToString();
                numberString = item.Substring(1);
            }

            var numbers = numberString.Split(',');
            var xString = numbers[0];
            var yString = numbers[1];

            X = Convert.ToDouble(xString);
            Y = Convert.ToDouble(yString);
        }

        public override string ToString()
        {
            if (Action == "Z") return "Z";
            return $"{Action}{X},{Y}";
        }
    }
}
