using ESLTracker.DataModel;
using ESLTracker.DataModel.Enums;
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

                game_path = ((string)Registry.GetValue(regdir, "Path", "")).Replace("\"", "");
                sent_path = Path.Combine(game_path, "sent.txt");
                sent_unused = Path.Combine(game_path, "sent_unused.txt");
            }
            catch (System.Exception ex)
            {
                throw ex;
            }
        }
        public string game_path;
        private string sent_path;
        private string sent_unused;

        private static readonly string[] draw_from_deck = { "player played medallion_presentRight",
                    "player played deck_present",
                    "player played mulligan_hand",
                    "player played surgeStart_reactionPile" };
        public static void UpdateGui(HashSet<CardInstance> cards, bool sendRed) { cards.ForEach(c => c.SendCardUpdated(sendRed)); }

        public bool ReadSentFile(HashSet<CardInstance> cards, HashSet<CardInstance> cards_silent)
        {
            if (!File.Exists(sent_path))
                return false;
            if (TrackerFactory.DefaultTrackerFactory.GetTracker().ActiveDeck == null)
                return false;

            try
            {
                PropertiesObservableCollection<CardInstance> activeDeck = TrackerFactory.DefaultTrackerFactory.GetTracker().
                                            ActiveDeck.SelectedVersion.Cards;
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
                        foreach (var currentCard in activeDeck)
                        {
                            currentCard.resetPlayed();
                            cards.Add(currentCard);
                        }
                        new TriggerChanceUpdater.TriggerChanceUpdater(activeDeck);
                        File.Delete(sent_path);
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

                            // only for played prophecy spells
                            if (draw_string == "player played surgeStart_reactionPile")
                            {
                                // don't allow read if that called last one
                                if (i == f.Count() - 1)
                                    return false;
                                    var prophecy = f[i + 1];
                                if (prophecy.Contains("someone played DefaultLerp card"))
                                {
                                    offset = f[i+1].IndexOf("card=") + ("card=").Length;
                                    played = f[i+1].Substring(offset);
                                }
                            }
                            CardInstance currentCard = activeDeck.Where(ci => ci.Card.Name == played).
                                DefaultIfEmpty(CardInstance.Unknown).FirstOrDefault();
                            if (currentCard != CardInstance.Unknown)
                            {
                                currentCard.incPlayed();
                                cards.Add(currentCard);
                                break;
                            }
                        }
                        new TriggerChanceUpdater.TriggerChanceUpdater(activeDeck, cards_silent);
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