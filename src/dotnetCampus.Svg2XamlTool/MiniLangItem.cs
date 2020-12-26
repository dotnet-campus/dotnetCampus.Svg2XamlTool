using System;

namespace dotnetCampus.Svg2XamlTool
{
    class MiniLangItem
    {
        public MiniLangItem(string item)
        {
            if (item == "Z")
            {
                Action = "Z";
                return;
            }

            string numberString = item;
            if (GeometryToolView.StandardPrefix.Contains(item[0]))
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

        public string Action { get; }
        public double X { get; set; }
        public double Y { get; set; }

        public override string ToString()
        {
            if (Action == "Z") return "Z";
            return $"{Action}{X},{Y}";
        }
    }
}