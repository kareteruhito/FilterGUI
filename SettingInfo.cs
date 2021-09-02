namespace FilterGUI
{
    public class SettingInfo
    {
        public int BlurNumberOfTimes{ get; set; } = 13;
        public int NonLocalMeanH{ get; set; } = 16;
        public int LaplacianKsize{ get; set; } = 0;
        public int UnsharpMaskingK{ get; set; } = 45;
        
        private string getSettingJsonPath()
        {
            var path = System.Reflection.Assembly.GetEntryAssembly().Location;
            var dir = System.IO.Path.GetDirectoryName(path);
            var settingJsonPath = System.IO.Path.Combine(dir, "setting.json");

            return settingJsonPath;
        }

        public void Save(string path="")
        {
            path = (path == "") ? getSettingJsonPath() : path;

            var jsonStr = System.Text.Json.JsonSerializer.Serialize(this);

            var encoding = System.Text.Encoding.GetEncoding("utf-8");
            using(var writer = new System.IO.StreamWriter(path, false, encoding))
            {
                writer.WriteLine(jsonStr);
            }

        }
        public bool Load(string path="")
        {
            path = (path == "") ? getSettingJsonPath() : path;
            if (System.IO.File.Exists(path) == false) return false;

            string jsonStr = "";
            var encoding = System.Text.Encoding.GetEncoding("utf-8");
            using(var reader = new System.IO.StreamReader(path, encoding))
            {
                jsonStr = reader.ReadToEnd();
            }

            var loadObj = System.Text.Json.JsonSerializer.Deserialize<SettingInfo>(jsonStr);

            var type = loadObj.GetType();

            foreach(var e in type.GetProperties())
            {
                var property = type.GetProperty(e.Name);
                var v = property.GetValue(loadObj);
                property.SetValue(this, v);
            }
            /*
            BlurNumberOfTimes.Value = s.BlurNumberOfTimes.Value;
            NonLocalMeanH.Value = s.NonLocalMeanH.Value;
            LaplacianKsize.Value = s.LaplacianKsize.Value;
            UnsharpMaskingK.Value = s.UnsharpMaskingK.Value;
            */
            return true;
        }
    }
}