using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace dotnetCampus.Svg2XamlTool
{
    /// <summary>
    /// 图标显示
    /// </summary>
    public partial class IconView : Window
    {
        protected List<IconModel> Icons = new List<IconModel>();

        public IconView(List<ResourceDictionary> dictionaries)
        {
            InitializeComponent();

            foreach (var dict in dictionaries)
            {
                AddDict(dict);
            }

            IconControl.ItemsSource = Icons;
            var cv = CollectionViewSource.GetDefaultView(IconControl.ItemsSource);
            cv.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
        }

        private void AddDict(ResourceDictionary dict)
        {
            var dictName = dict.Source.ToString().Split('/').Last();
            var drawingImages = dict.OfType<DictionaryEntry>().Where(de => de.Value is DrawingImage)
                .Select(de => new IconModel { DrawingImage = (DrawingImage)de.Value, Key = (string)de.Key, Group = dictName, Source = dict.Source.AbsolutePath }).OrderBy(de => de.Key).ToList();

            Icons.AddRange(drawingImages);
            var drawingImages2 = dict.OfType<DictionaryEntry>().Where(de => de.Value is Geometry).Select(de =>
            {
                var gd = new GeometryDrawing { Pen = null, Geometry = (Geometry)de.Value };
                BindingOperations.SetBinding(gd, GeometryDrawing.BrushProperty, new Binding("Foreground") { Source = this });
                return new IconModel
                {
                    Key = (string)de.Key,
                    Group = dictName,
                    DrawingImage = new DrawingImage(gd),
                    Source = dict.Source.AbsolutePath
                };
            }).OrderBy(de => de.Key);

            Icons.AddRange(drawingImages2);
        }
    }

    [DebuggerDisplay("{Key},{Group}")]
    public class IconModel
    {
        public DrawingImage DrawingImage { get; set; }
        public string Key { get; set; }
        public string Group { get; set; }
        public string Source { get; set; }
    }
}
