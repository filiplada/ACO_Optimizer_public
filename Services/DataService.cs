using ACO_Optimizer.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACO_Optimizer.Services
{
    public static class DataService
    {
        public static List<Station> LoadStationsFromJson(string jsonPath)
        {
            string jsonpath = AppDomain.CurrentDomain.BaseDirectory + jsonPath;

            // Read as UTF-8 explicitly to preserve special characters in Polish names
            var json = File.ReadAllText(jsonpath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<Station>>(json);
        }
    }
}




