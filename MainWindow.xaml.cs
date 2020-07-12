using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;

namespace Harvester
{

    public static class StringExtensions
    {
        public static bool ContainsOwn(this string name, string cont)
        {
            return name.Contains(cont, System.StringComparison.OrdinalIgnoreCase);
        }
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static readonly string xmlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Harvester", "Crafts.xaml");
        public ObservableCollection<HarvestData> Harvests { get; set; }

        public MainWindow()
        {
            Harvests = new ObservableCollection<HarvestData>();

            InitializeComponent();
            DataContext = this;

            DataObject.AddPastingHandler(copyfield, OnPaste);
            Loaded += MainWindow_Loaded;
            Closed += delegate { Save_button(null, null); };
            
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(xmlPath))
            {
                try
                {


                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(xmlPath);
                    foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
                    {
                        var harvest = new HarvestData
                        {
                            Name = node.ChildNodes[0].InnerText,
                            Count = Convert.ToInt32(node.ChildNodes[1].InnerText),
                            Type = node.ChildNodes[2].InnerText,
                            Price = node.ChildNodes[3].InnerText,
                            Comment = node.ChildNodes[4].InnerText,
                            Lock = Convert.ToBoolean(node.ChildNodes[5].InnerText),
                            CraftType = (CraftTypes)Enum.Parse(typeof(CraftTypes), node.ChildNodes[6].InnerText)
                        };
                        Harvests.Add(harvest);
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Please delete your previous datafile from Documents/Harvester and restart the application");
                }
            }


        }

        public void Save_button(object sender, RoutedEventArgs e)
        {
            if (!Harvests.Any())
            {
                return;
            }

            _ = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Harvester"));

            using XmlWriter writer = XmlWriter.Create(xmlPath);

            writer.WriteStartElement("Craft");

            foreach (var item in datagrid.ItemsSource as ObservableCollection<HarvestData>)
            {
                writer.WriteStartElement("Node");

                writer.WriteElementString("Name", item.Name);
                writer.WriteElementString("Count", item.Count.ToString());
                writer.WriteElementString("Type", item.Type);
                writer.WriteElementString("Price", item.Price);
                writer.WriteElementString("Comment", item.Comment);
                writer.WriteElementString("Lock", item.Lock.ToString());
                writer.WriteElementString("CraftTypes", item.CraftType.ToString());

                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.Flush();
            writer.Close();
        }


        public enum CraftTypes
        {
            Augment,
            Remove,
            RemoveAndAug,
            RemoveNonAndAug,
            AddInfluence,//
            ChangeRes,//
            Enchant,//
            Reroll,//
            Reforge,//
            Special//
        }

        public class HarvestData : INotifyPropertyChanged
        {
            [Display(AutoGenerateField = false)]
            public bool Lock { get; set; }
            public string Name { get; set; }

            public bool Hidden { get; set; }

            public CraftTypes CraftType { get; set; }

            private int Count_ { get; set; }
            public int Count
            {
                get
                {
                    return Count_;
                }
                set
                {
                    if (Lock) return;
                    if (value < 0)
                    {
                        Count_ = 0;
                    }
                    else
                    {
                        Count_ = value;
                    }
                    OnPropertyChanged();
                }
            }
            private string Type_ { get; set; }
            public string Type { get
                {
                    return Type_;
                }
                set
                {
                    CraftType = CraftTypeEnumCheck(value);
                    Type_ = value;
                }
            }
            public string Price { get; set; }
            public string Comment { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string info = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
            }
        }

        public static CraftTypes CraftTypeEnumCheck(string type)
        {
            return type switch
            {
                var d when d.ContainsOwn("Enchant") => CraftTypes.Enchant,
                var d when d.ContainsOwn("Reforge") => CraftTypes.Reforge,
                var d when d.ContainsOwn("Reroll") => CraftTypes.Reroll,
                var d when d.ContainsOwn("Change") => CraftTypes.ChangeRes,
                var d when d.ContainsOwn("Add Influence") => CraftTypes.AddInfluence,
                var d when d.ContainsOwn("Non-") => CraftTypes.RemoveNonAndAug,
                var d when d.ContainsOwn("Remove") && !d.ContainsOwn("Augment") => CraftTypes.Remove,
                var d when d.ContainsOwn("Remove") && d.ContainsOwn("Augment") => CraftTypes.RemoveAndAug,
                var d when d.ContainsOwn("Augment") => CraftTypes.Augment,
                _ => CraftTypes.Special
            };
        }

        public string CheckBaseType(string name)
        {
            return name switch
            {
                var d when d.ContainsOwn("Remove") => RemoveType(name),
                var d when d.ContainsOwn("Augment") => AugmentType(name),
                var d when d.ContainsOwn("Change") => ChangeType(name),
                var d when d.ContainsOwn("Reroll") => RerollType(name),
                var d when d.ContainsOwn("Enchant") => EnchantType(name),
                var d when d.ContainsOwn("Reforge") => ReforgeType(name),
                var d when d.ContainsOwn("Influence") => InfluenceType(name), // works because augment is check before influence,
                _ => "Special"
            };
        }



        private string CraftTypeBase(string name)
        {

            return name.ToLower() switch
            {
                var d when d.ContainsOwn("Physical") => "Physical",
                var d when d.ContainsOwn("Attack") => "Attack",
                var d when d.ContainsOwn("Lightning") => "Lightning",
                var d when d.ContainsOwn("Non-Lightning") => "Non-Lightning",
                var d when d.ContainsOwn("Non-Chaos") => "Non-Chaos",
                var d when d.ContainsOwn("Cold") => "Cold",
                var d when d.ContainsOwn("Non-Cold") => "Non-Cold",
                var d when d.ContainsOwn("Defence") => "Defence",
                var d when d.ContainsOwn("Life") => "Life",
                var d when d.ContainsOwn("Caster") => "Caster",
                var d when d.ContainsOwn("Chaos") => "Chaos",
                var d when d.ContainsOwn("Non-Chaos") => "Non-Chaos",
                var d when d.ContainsOwn("Fire") => "Fire",
                var d when d.ContainsOwn("Non-Fire") => "Non-Fire",
                var d when d.ContainsOwn("Speed") => "Speed",
                var d when d.ContainsOwn("White") => "White socket",
                var d when d.ContainsOwn("Non-Speed") => "Non-Speed",
                var d when d.ContainsOwn("Non-Life") => "Non-Life",
                var d when d.ContainsOwn("Non-Physical") => "Non-Physical",
                var d when d.ContainsOwn("Non-Defence") => "Non-Defence",
                var d when d.ContainsOwn("Non-Attack") => "Non-Attack",
                var d when d.ContainsOwn("Non-Caster") => "Non-Caster",
                var d when d.ContainsOwn("Critical") => "Critical",
                var d when d.ContainsOwn("Influence") => "Influence",
                var d when d.ContainsOwn("Suffix") => "Suffix",
                var d when d.ContainsOwn("Prefix") => "Prefix",
                _ => "unknown"
            };
        }


        private (string, string) CraftTypeMixed(string name)
        {
            var list = new List<string>
            {
                "Physical",
                "Chaos",
                "Attack",
                "Map",
                "Suffix",
                "Prefix",
                "Lightning",
                "Cold",
                "Defence",
                "Life",
                "Unique",
                "Caster",
                "Fire",
                "Speed",
                "Elder Fragment",
                "Shaper Fragment",
                "Critical",
                "Influence"

            };
            var nonlist = new List<string>
            {
                "Non-Physical",
                "Non-Defence",
                "Non-Lightning",
                "Non-Chaos",
                "Non-Caster",
                "Non-Cold",
                "Non-Speed",
                "Non-Attack",
                "Non-Fire",
                "Non-Life"
            };

            string[] init = new string[2];
            foreach (var item in nonlist)
            {
                if (name.ContainsOwn(item))
                {
                    init[0] = item;
                    break;
                }
            }

            if (init[0] == null)
            {
                int i = 0;
                foreach (var item in list)
                {
                    if (name.ContainsOwn(item))
                    {
                        init[i] = item;
                        i++;
                        if (i == 2)
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                var subs = name.Substring(name.IndexOf(init[0], StringComparison.OrdinalIgnoreCase)).Substring(init[0].Length);
                foreach (var item in list)
                {
                    if (subs.ContainsOwn(item))
                    {
                        init[1] = item;
                        break;
                    }
                }
            }

            if (init[1] == null)
            {
                return (init[0], init[0]);
            }
            else
            {
                if (name.IndexOf(init[0], StringComparison.OrdinalIgnoreCase) < name.IndexOf(init[1], StringComparison.OrdinalIgnoreCase))
                {
                    return (init[0], init[1]);
                }
                else
                {
                    return (init[1], init[0]);
                }
            }
        }

        private string RemoveType(string name)
        {
            if (name.ContainsOwn("add"))
            {
                return MixedType(name);
            }

            return "Remove " + CraftTypeBase(name);
        }

        private string ChangeType(string name)
        {

            var ret = CraftTypeMixed(name);
            return $"Change {ret.Item1} to {ret.Item2}";
        }
        private string RerollType(string name)
        {

            var ret = CraftTypeMixed(name);
            var bass = "Reroll ";
            if (ret.Item1 == ret.Item2)
            {
                bass += ret.Item1;
            }
            else
            {
                bass += ret.Item1 + "," + ret.Item2;
            }
            
            if (name.ContainsOwn("Lucky"))
            {
                bass += " Lucky";
            }
            return bass;
        }

        private string EnchantType(string name)
        {

            //var ret = CraftTypeBase(name);
            var firststring = name.Split(".")[0];
            
            var bass = $"Enchant ";
            bass += firststring switch
            {
                var d when d.ContainsOwn("Weapon") => "Weapon",
                var d when d.ContainsOwn("Armour") => "Armour",
                var d when d.ContainsOwn("Map") => "Map",
                var d when d.ContainsOwn("Flask") => "Flask",
                var d when d.ContainsOwn("Jewelry") => "Jewelry",
                var d when d.ContainsOwn("Jewel") => "Jewel",
                _ => "unknown"
            };
            return bass;
        }

        private string ReforgeType(string name)
        {

            //var ret = CraftTypeBase(name);
            var basss = CraftTypeBase(name);

            var bass = $"Reforge {basss} ";
            if (name.ContainsOwn("Common"))
            {
                bass += "Lucky";
            }
            
            return bass;
        }

        private string InfluenceType(string name)
        {
            var basss = CraftTypeBase(name);

            var bass = $"Add {basss} ";

            return bass;
        }

        private string AugmentType(string name)
        {
            var basse = "Augment " + CraftTypeBase(name);
            if (name.ContainsOwn("Lucky"))
            {
                basse += " Lucky";
            }
            return basse;
        }

        private string MixedType(string name)
        {
            var ret = CraftTypeMixed(name);
            return $"Remove {ret.Item1} / Augment {ret.Item2}";
        }


        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            var isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText) return;

            var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;

            var rows = text.Split("\r\n");
            if (rows.Length < 4)
            {
                return;
            }

            for (int i = 7; i < rows.Length; i++)
            {
                if (rows[i].Contains("--"))
                {
                    break;
                }
                var strings = rows[i].Remove(rows[i].Length - 14);

                if (Harvests.Any(p => p.Name == strings))
                {
                    Harvests.First(p => p.Name == strings).Count++;
                }
                else
                {
                    Harvests.Add(new HarvestData
                    {
                        Comment = "Write a comment here!",
                        Count = 1,
                        Name = strings,
                        Price = "40c",
                        Type = CheckBaseType(strings)
                    });
                }
            }
            e.Handled = true;
        }

        private void Copyfield_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ((TextBox)sender).Clear();
            e.Handled = true;
        }

        private void Copyfield_KeyUp(object sender, KeyEventArgs e)
        {
            ((TextBox)sender).Clear();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

            var list = Harvests.Where(p => !p.Lock).ToList();
            foreach (var item in list)
            {
                Harvests.Remove(item);
            }

        }

        private void Row_clicked(object sender, RoutedEventArgs e)
        {
            if (!(((Button)sender).DataContext is HarvestData context))
            {
                return;
            }
            var h = Harvests.First(p => p.Name == context.Name);
            h.Count--;

        }

        private void Row_clicked_plus(object sender, RoutedEventArgs e)
        {
            if (!(((Button)sender).DataContext is HarvestData context))
            {
                return;
            }
            var h = Harvests.First(p => p.Name == context.Name);

            h.Count++;

        }

        private void LockRow_click(object sender, RoutedEventArgs e)
        {
            if (!(((Button)sender).DataContext is HarvestData context))
            {
                return;
            }
            var h = Harvests.First(p => p.Name == context.Name);
            h.Lock ^= true;

            if (h.Lock)
            {
                ((Button)sender).Background = Brushes.Red;
                ((Button)sender).Content = "Unlock!";
            }
            else
            {
                ((Button)sender).Background = Brushes.LightGreen;
                ((Button)sender).Content = "Lock!";

            }

        }

        private void CopyClipboard(object sender, RoutedEventArgs e)
        {
            var b = new StringBuilder();

            //var harvestbase = Harvests.Where(p => !p.Lock && p.Count != 0 && p.Type != "Special" && !p.Type.ContainsOwn("Change"));

            //var changebase = Harvests.Where(p => !p.Lock && p.Count != 0 && p.Type.ContainsOwn("Change"));

            //var notmixed = harvestbase.Where(p => !p.Type.ContainsOwn("/"));

            //var mixed = harvestbase.Where(p => p.Type.ContainsOwn("/") && !p.Type.ContainsOwn("Non"));
            //var mixedNon = harvestbase.Where(p => p.Type.ContainsOwn("/") && p.Type.ContainsOwn("Non"));
            b.Append("**WTS**: \r");

            if (Harvests.Any(p => p.CraftType == CraftTypes.Augment && !p.Lock && p.Count != 0))
            {
                b.Append("**Augment**: \r");


                foreach (var item in Harvests.Where(p => p.CraftType == CraftTypes.Augment && !p.Lock && p.Count != 0))
                {
                    b.Append($"-{item.Type.Split(" ")[1]} : **{item.Price}** \t {item.Count}x \r\n");
                }
            }

            if (Harvests.Any(p => p.CraftType == CraftTypes.Remove && !p.Lock && p.Count != 0))
            {
                b.Append("\r\n **Remove**: \r");

                foreach (var item in Harvests.Where(p => p.CraftType == CraftTypes.Remove && !p.Lock && p.Count != 0))
                {
                    b.Append($"-{item.Type.Split(" ")[1]} : **{item.Price}** \t {item.Count}x \r\n");
                }
            }

            if (Harvests.Any(p => p.CraftType == CraftTypes.RemoveAndAug && !p.Lock && p.Count != 0))
            {
                b.Append("\r\n **Remove / Augment**: \r");

                foreach (var item in Harvests.Where(p => p.CraftType == CraftTypes.RemoveAndAug && !p.Lock && p.Count != 0))
                {
                    b.Append($"-{item.Type} : **{item.Price}** \t {item.Count}x \r\n");
                }
            }

            if (Harvests.Any(p => p.CraftType == CraftTypes.RemoveNonAndAug && !p.Lock && p.Count != 0))
            {
                b.Append("\r\n **Remove NON / Augment**: \r ");

                foreach (var item in Harvests.Where(p => p.CraftType == CraftTypes.RemoveNonAndAug && !p.Lock && p.Count != 0))
                {
                    b.Append($"-{item.Type} : **{item.Price}** \t {item.Count}x \r\n");
                }
            }

            if (Harvests.Any(p => p.CraftType == CraftTypes.ChangeRes && !p.Lock && p.Count != 0))
            {
                b.Append("\r\n **Change mods**: \r ");

                foreach (var item in Harvests.Where(p => p.CraftType == CraftTypes.ChangeRes && !p.Lock && p.Count != 0))
                {
                    b.Append($"-{item.Type} : **{item.Price}** \t {item.Count}x \r\n");
                }
            }

            if (Harvests.Any(p => p.CraftType == CraftTypes.Enchant && !p.Lock && p.Count != 0))
            {
                b.Append("\r\n **Enchants**: \r ");

                foreach (var item in Harvests.Where(p => p.CraftType == CraftTypes.Enchant && !p.Lock && p.Count != 0))
                {
                    b.Append($"-{item.Type} : **{item.Price}** \t {item.Count}x \r\n");
                }
            }

            if (Harvests.Any(p => p.CraftType == CraftTypes.AddInfluence && !p.Lock && p.Count != 0))
            {
                b.Append("\r\n **Add Influence**: \r ");

                foreach (var item in Harvests.Where(p => p.CraftType == CraftTypes.AddInfluence && !p.Lock && p.Count != 0))
                {
                    b.Append($"-{item.Type} : **{item.Price}** \t {item.Count}x \r\n");
                }
            }

            if (Harvests.Any(p => p.CraftType == CraftTypes.Reroll && !p.Lock && p.Count != 0))
            {
                b.Append("\r\n **Rerolls**: \r ");

                foreach (var item in Harvests.Where(p => p.CraftType == CraftTypes.Reroll && !p.Lock && p.Count != 0))
                {
                    b.Append($"-{item.Type} : **{item.Price}** \t {item.Count}x \r\n");
                }
            }

            if (Harvests.Any(p => p.CraftType == CraftTypes.Reforge && !p.Lock && p.Count != 0))
            {
                b.Append("\r\n **Reforges**: \r ");

                foreach (var item in Harvests.Where(p => p.CraftType == CraftTypes.Reforge && !p.Lock && p.Count != 0))
                {
                    b.Append($"-{item.Type} : **{item.Price}** \t {item.Count}x \r\n");
                }
            }

            if (Harvests.Any(p => p.CraftType == CraftTypes.Special && !p.Lock && p.Count != 0))
            {
                b.Append("\r\n **Special crafts**: \r ");

                foreach (var item in Harvests.Where(p => p.CraftType == CraftTypes.Special && !p.Lock && p.Count != 0))
                {
                    b.Append($"-{item.Name} : **{item.Price}** \t {item.Count}x \r\n");
                }
            }



            Clipboard.SetText(b.ToString());
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9]+").IsMatch(e.Text);
        }

        private void CopyRow(object sender, RoutedEventArgs e)
        {
            if (!(((Button)sender).DataContext is HarvestData context))
            {
                return;
            }

            if (context.Type.ContainsOwn("Special"))
            {
                Clipboard.SetText($"Hi. I'm selling \r\n **{context.Name}** \r\n for **{context.Price}** {context.Count}x");
            }
            else
            {
                Clipboard.SetText($"Hi. I'm selling **{context.Type}** for **{context.Price}** {context.Count}x");
            }
        }

        private void OpenUserControl(object sender, RoutedEventArgs e)
        {
            var window = new PoeStashWindow();
            window.ShowDialog();
        }

        private void DeleteRow(object sender, RoutedEventArgs e)
        {
            if (!(((Button)sender).DataContext is HarvestData context))
            {
                return;
            }

            Harvests.Remove(context);
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var box = (ComboBox)sender;
            var selected = (ComboBoxItem)box.SelectedItem;

            var type = (string)selected.Content;

            if(type == "All" || string.IsNullOrEmpty(type))
            {
                foreach (var item in Harvests)
                {
                    item.Hidden = false;
                }
                datagrid.Items.Refresh();
                return;
            }
            var en = CraftTypeEnumCheck(type);
            foreach (HarvestData item in Harvests)
            {
                if (item.CraftType != en)
                {
                    item.Hidden = true;
                }
                else
                {
                    item.Hidden = false;
                }
            }

            datagrid.Items.Refresh();
        }
    }

    public class LockToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if(value is bool ret)
            {
                return ret ? Brushes.Red : Brushes.LightGreen;
            }
            return Brushes.LightGreen;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is SolidColorBrush boolean)
            {
                if (boolean == Brushes.LightGreen)
                    return true;
                else
                    return false;
            }
            return false;
        }
    }

    public class LockToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool ret)
            {
                return ret ? "Unlock!" : "Lock!";
            }
            return "Lock!";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string boolean)
            {
                if (boolean == "Lock!")
                    return true;
                else
                    return false;
            }
            return false;
        }
    }

    public class CountToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int ret)
            {
                return ret == 0 ? Brushes.Red : Brushes.LightGreen;
            }
            return Brushes.LightGreen;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) // this is useless, 
        {
            if (value is SolidColorBrush boolean)
            {
                if (boolean == Brushes.LightGreen)
                    return 0;
                else
                    return 1;
            }
            return false;
        }
    }

    public class CountToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int ret)
            {
                return ret == 0 ? "Out of Stock!" : "Sold!";
            }
            return "Lock!";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) //useless
        {
            if (value is string boolean)
            {
                if (boolean == "Sold!")
                    return 0;
                else
                    return 1;
            }
            return false;
        }
    }

}
