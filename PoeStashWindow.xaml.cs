using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        MainWindow main { get; set; }
        public PoeStashWindow()
        {
            main = App.Current.Windows.OfType<MainWindow>().First();
            InitializeComponent();
        }

        private async void FinishButton(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.IsEnabled = false;
            userPlane.Visibility = Visibility.Collapsed;
            dataRec.Visibility = Visibility.Visible;
            if (string.IsNullOrEmpty(PoeSessIdText.Text) || string.IsNullOrEmpty(UserNameText.Text) || string.IsNullOrEmpty(StashTabIndex.Text))
            {
                MessageBox.Show("Please fill every TextBox!");
                button.IsEnabled = true;
                userPlane.Visibility = Visibility.Visible;
                dataRec.Visibility = Visibility.Collapsed;
                return;
            }

            if (!int.TryParse(StashTabIndex.Text, out int res))
            {
                MessageBox.Show("Please user a number for the Stash Tab Index.");
                button.IsEnabled = true;
                userPlane.Visibility = Visibility.Visible;
                dataRec.Visibility = Visibility.Collapsed;
                return;
            }

            if (res <= 0)
            {
                MessageBox.Show("Stash Tab Index can't be negative or zero!");
                button.IsEnabled = true;
                userPlane.Visibility = Visibility.Visible;
                dataRec.Visibility = Visibility.Collapsed;
                return;
            }
            var baseAddress = new Uri("https://www.pathofexile.com/character-window/get-stash-items?league=Harvest&tabs=0&tabIndex=" + (res - 1).ToString() + "&accountName=" + UserNameText.Text);
            var cookieContainer = new CookieContainer();
            using var handler = new HttpClientHandler() { CookieContainer = cookieContainer };

            using var client = new HttpClient(handler) { BaseAddress = baseAddress };
            client.DefaultRequestHeaders.Add("User-Agent", "Harvest Beta 3_1 Horticrafting station extraction tool");
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
                        MessageBox.Show("Couldn't find any stations in the selected stash tab!");
                        button.IsEnabled = true;
                        userPlane.Visibility = Visibility.Visible;
                        dataRec.Visibility = Visibility.Collapsed;
                        return;
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
                                foreach (var mod in mods)
                                {
                                    var modname = (string)mod;
                                    modname = modname.Replace("{", string.Empty).Replace("}", string.Empty).Replace("<white>", string.Empty);
                                    modname = modname.Remove(modname.Length - 4, 4);


                                    if (main.Harvests.Any(p => p.Name == modname))
                                    {
                                        main.Harvests.First(p => p.Name == modname).Count++;
                                    }
                                    else
                                    {
                                        main.Harvests.Add(new HarvestData
                                        {
                                            Comment = "Write a comment here!",
                                            Count = 1,
                                            Name = modname,
                                            Price = "40c",
                                            Type = main.CheckBaseType(modname)
                                        });
                                    }

                                    //<white>{Remove} a random <white>{non-Life} modifier from an item and <white>{add} a new <white>{Life} modifier (78)
                                }
                            }
                        }
                    }
                }
                catch
                {
                    MessageBox.Show("Requesting Stash Tabs failed. Either the service is not available or your credentials are incorrect!");
                    button.IsEnabled = true;
                    userPlane.Visibility = Visibility.Visible;
                    dataRec.Visibility = Visibility.Collapsed;
                    return;
                }
                MessageBox.Show("Successfully loaded the stash tab!");

                App.Current.Windows.OfType<PoeStashWindow>().First().Close();





            }
            else
            {
                MessageBox.Show("Requesting Stash Tabs failed. Either the service is not available or your credentials are incorrect!");
                button.IsEnabled = true;
                userPlane.Visibility = Visibility.Visible;
                dataRec.Visibility = Visibility.Collapsed;
            }


        }
    }
}
