using ESLTracker.DataModel;
using LiveCharts.Helpers;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ESLTracker.Utils.DeckFileReader
{
    public class DeckFileReader
    {
        public DeckFileReader()
        {
            try
            {
                const string regdir =
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\The Elder Scrolls Legends";

                string path = ((string)Registry.GetValue(regdir, "Path", "")).Replace("\"", "");
                sent_path = Path.Combine(path, "sent.txt");
                sent_unused = Path.Combine(path, "sent_unused.txt");
            }
            catch (System.Exception ex)
            {
                throw ex;
            }
        }

        private string sent_path;
        private string sent_unused;

        private static readonly string[] draw_from_deck = { "player played medallion_presentRight",
                    "player played deck_present",
                    "player played mulligan_hand" };
        public static void UpdateGui(HashSet<CardInstance> cards, bool sendRed) { cards.ForEach(c => c.SendCardCountUpdated(sendRed)); }

        public bool ReadSentFile(HashSet<CardInstance> cards)
        {
            if (!File.Exists(sent_path))
                return false;

            try
            {
                string[] f = File.ReadAllLines(sent_path);
                int i = f.Count() - 1;
                for (; i > 0; --i)
                {
                    if (f[i].Contains("=== Started Match"))
                    {
                        if (i != f.Count() - 1)
                            i++;
                        break;
                    }
                }
                string unused = "";
                for (; i < f.Count(); ++i)
                {
                    if (f[i].Contains("=== Ended Match"))
                    {
                        foreach (var c in TrackerFactory.DefaultTrackerFactory.GetTracker().
                            ActiveDeck.SelectedVersion.Cards)
                        {
                            c.resetPlayed();
                            cards.Add(c);
                        }
                        return true;
                    }

                    bool draw_found = false;
                    draw_from_deck.ForEach(draw_string => draw_found = draw_found || f[i].Contains(draw_string));
                    if (draw_found)
                    {
                        foreach (var draw_string in draw_from_deck)
                        {
                            if (!f[i].Contains(draw_string))
                                continue;
                            int offset = f[i].IndexOf("card=") + ("card=").Length;
                            string played = f[i].Substring(offset);
                            CardInstance currentCard = TrackerFactory.DefaultTrackerFactory.GetTracker().
                                ActiveDeck.SelectedVersion.Cards.Where(ci => ci.Card.Name == played).
                                DefaultIfEmpty(CardInstance.Unknown).FirstOrDefault();
                            if (currentCard != CardInstance.Unknown)
                            {
                                currentCard.incPlayed();
                                cards.Add(currentCard);
                                break;
                            }
                        }
                    }
                    else
                        unused += f[i] + "\n";

                }
                File.Delete(sent_path);
                File.AppendAllText(sent_unused, unused);
            }
            catch
            {
            }
            return false;
        }
    }
}