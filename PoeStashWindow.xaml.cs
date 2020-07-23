using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Harvester.MainWindow;

namespace Harvester
{
    /// <summary>
    /// Interaction logic for PoeStashWindow.xaml
    /// </summary>
    public partial class PoeStashWindow : Window
    {
        static readonly string xmlPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Harvester", "UpdateDetails.txt");
        MainWindow Main { get; set; }

        private class UpdateDetails
        {
            public string Sessionid { get; set; }
            public string StashTabs { get; set; }
            public string UserName { get; set; }
        }
        private UpdateDetails LatestDetails { get; set; }
        public PoeStashWindow()
        {
            Main = App.Current.Windows.OfType<MainWindow>().First();
            InitializeComponent();
            ReadLastLogin();
        }

        private void ReadLastLogin()
        {
            try
            {
                if (File.Exists(xmlPath))
                {
                    var contents = File.ReadAllText(xmlPath);
                    var split = contents.Split(";");
                    var details = new UpdateDetails
                    {
                        Sessionid = split[0],
                        StashTabs = split[1],
                        UserName = split[2]
                    };
                    LatestDetails = details;

                    detailsButton.Visibility = Visibility.Visible;
                }
            }
            catch(Exception)
            {
                MessageBox.Show("UpdateDetails file changed. Please delete it from documents/harvester :)");
            }
        }

        private void SaveLastLogin()
        {
            try
            {
                using var writer = new StreamWriter(xmlPath, append: false);
                writer.Write($"{LatestDetails.Sessionid};{LatestDetails.StashTabs};{LatestDetails.UserName}");
                writer.Close();
            }
            catch (Exception)
            {
                return;
            }
        }

        private async void FinishButton(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.IsEnabled = false;
            userPlane.Visibility = Visibility.Collapsed;
            dataRec.Visibility = Visibility.Visible;
            detailsButton.IsEnabled = false;
            if (string.IsNullOrEmpty(PoeSessIdText.Text) || string.IsNullOrEmpty(UserNameText.Text) || string.IsNullOrEmpty(StashTabIndex.Text))
            {
                MessageBox.Show("Please fill every TextBox!");
                button.IsEnabled = true;
                detailsButton.IsEnabled = true;
                userPlane.Visibility = Visibility.Visible;
                dataRec.Visibility = Visibility.Collapsed;
                return;
            }

            if(!new Regex(@"^[0-9]+(\,[0-9]+)*$").IsMatch(StashTabIndex.Text))
            {
                MessageBox.Show("Please use valid numbers for the Stash Tab Index.");
                button.IsEnabled = true;
                detailsButton.IsEnabled = true;
                userPlane.Visibility = Visibility.Visible;
                dataRec.Visibility = Visibility.Collapsed;
                return;
            }
            List<string> errors = new List<string>();

            Main.Harvests.Clear();

            foreach (var split in StashTabIndex.Text.Split(","))
            {
                if(!int.TryParse(split,out int res))
                {
                    errors.Add($"{split} is not a valid number for stash index \r\n");
                    continue;
                }
                if(res == 0)
                {
                    errors.Add($"Stash tab index cannot be 0 or negative! \r\n");
                    continue;
                }
                var baseAddress = new Uri("https://www.pathofexile.com/character-window/get-stash-items?league=Harvest&tabs=0&tabIndex=" + (res-1).ToString() + "&accountName=" + UserNameText.Text);
                var cookieContainer = new CookieContainer();
                using var handler = new HttpClientHandler() { CookieContainer = cookieContainer };

                using var client = new HttpClient(handler) { BaseAddress = baseAddress };
                client.DefaultRequestHeaders.Add("User-Agent", "Harvest Beta 3_3 Horticrafting station extraction tool githubdotcomslashJeyR01slashHarvester");
                cookieContainer.Add(baseAddress, new Cookie("POESESSID", PoeSessIdText.Text));
                var result = await client.PostAsync(baseAddress, null);

                if (result.IsSuccessStatusCode)
                {
                    var resp = await result.Content.ReadAsStringAsync();
                    try
                    {
                        JObject jsont = JObject.Parse(resp);

                        JArray array = (JArray)jsont["items"];

                        if (array == null || array.Count == 0)
                        {
                            errors.Add($"Couldn't find any items in the selected stash tab ({split})! \r\n");
                            Thread.Sleep(1000);
                            continue;
                        }

                        foreach (var item in array)
                        {
                            var nametoken = item["typeLine"];
                            if (nametoken == null)
                            {
                                continue;
                            }
                            var name = (string)nametoken;
                            if (name != null && name == "Horticrafting Station")
                            {
                                var mods = (JArray)item["craftedMods"];
                                if (mods != null && mods.Count != 0)
                                {
                                    var note = item["note"];
                                    string[] priceNotes = new string[3] { "-", "-", "-" };
                                    if(note != null)
                                    {
                                        try
                                        {
                                            var notsplit = (string)note;
                                            if (notsplit.Count(p => p == '/') == 2)
                                            {
                                                priceNotes = notsplit.Split("/");
                                            }
                                           
                                        }
                                        catch (Exception)
                                        {

                                        }
                                    }
                                    for (int i = 0; i < mods.Count; i++)
                                    {
                                        var modname = (string)mods[i];
                                        modname = modname.Replace("{", string.Empty).Replace("}", string.Empty).Replace("<white>", string.Empty);
                                        modname = modname.Remove(modname.Length - 4, 4);


                                        if (Main.Harvests.Any(p => p.Name == modname))
                                        {
                                            var craft = Main.Harvests.First(p => p.Name == modname);
                                            craft.Count++;
                                            if(priceNotes[i] != "-")
                                            {
                                                craft.SetPrice(priceNotes[i]);
                                            }
                                        }
                                        else
                                        {
                                            var craft = new HarvestData(Main.CheckBaseType(modname))
                                            {
                                                Comment = "Write a comment here!",
                                                Count = 1,
                                                Name = modname,
                                                //Price = "40c",
                                                //Type = Main.CheckBaseType(modname)
                                            };
                                            Main.Harvests.Add(craft);

                                            if (priceNotes[i] != "-")
                                            {
                                                craft.SetPrice(priceNotes[i]);
                                            }
                                        }

                                        //<white>{Remove} a random <white>{non-Life} modifier from an item and <white>{add} a new <white>{Life} modifier (78)
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        errors.Add($"Requesting Stash Tab {split} failed. \r\n");
                    }
                    
                }
                else
                {
                    errors.Add($"Requesting Stash Tab {split} failed. Webserver responded with statuscode {result.StatusCode} \r\n");
                    //button.IsEnabled = true;
                    //userPlane.Visibility = Visibility.Visible;
                    //dataRec.Visibility = Visibility.Collapsed;
                    //break;
                }

                Thread.Sleep(1000); // as to not hammer the API
            }

            LatestDetails = new UpdateDetails
            {
                Sessionid = PoeSessIdText.Text,
                StashTabs = StashTabIndex.Text,
                UserName = UserNameText.Text
            };
            SaveLastLogin();

            if (errors.Any(p=>p!= null))
            {
                var stringb = new StringBuilder();
                foreach (var item in errors.Where(p=>p!= null))
                {
                    stringb.Append(item);
                }
                MessageBox.Show(stringb.ToString());
                button.IsEnabled = true;
                detailsButton.IsEnabled = true;
                userPlane.Visibility = Visibility.Visible;
                dataRec.Visibility = Visibility.Collapsed;

            }
            else
            {
                if (Main.Harvests.Any())
                {
                    MessageBox.Show("Successfully loaded the stash tabs!");
                    App.Current.Windows.OfType<PoeStashWindow>().First().Close();
                }
                else
                {
                    MessageBox.Show("Couldn't find any horti stations in the selectd stash tabs.");
                    button.IsEnabled = true;
                    detailsButton.IsEnabled = true;
                    userPlane.Visibility = Visibility.Visible;
                    dataRec.Visibility = Visibility.Collapsed;
                }

            }



        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            UserNameText.Text = LatestDetails.UserName;
            PoeSessIdText.Text = LatestDetails.Sessionid;
            StashTabIndex.Text = LatestDetails.StashTabs;
        }
    }
}
