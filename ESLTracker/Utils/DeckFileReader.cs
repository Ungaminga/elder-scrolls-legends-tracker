using ESLTracker.Controls.Decks;
using ESLTracker.DataModel;
using ESLTracker.DataModel.Enums;
using ESLTracker.Utils.Messages;
using LiveCharts.Helpers;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System;

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
                decks_directory = Path.Combine(game_path, "decks");
                game_src_lib = Path.Combine(game_path, "The Elder Scrolls Legends_Data\\Managed\\game-src.dll");
                deck_selection = Path.Combine(game_path, "deck_selection.txt");
            }
            catch (System.Exception ex)
            {
                throw ex;
            }
        }
        public readonly string game_path;
        private string sent_path;
        private string sent_unused;
        private string decks_directory;
        public readonly string game_src_lib;
        private string deck_selection;

        private bool game_started = false;
        public bool isGameStarted() { return game_started; }

        private static readonly string[] draw_from_deck = { "player played medallion_presentRight",
                    "player played deck_present ", // we need put space after because there are deck_present_fast which not had been handled
                    "player played mulligan_hand",
                    "player played surgeStart_reactionPile",
                    "player played multiPresent_hand"};

        public static void UpdateGui(HashSet<CardInstance> cards, bool sendRed) { cards.ForEach(c => c.SendCardUpdated(sendRed)); }

        private bool wait_for_prohpecy = false;
        public bool ReadSentFile(HashSet<CardInstance> cards, HashSet<CardInstance> cards_silent)
        {
            if (!File.Exists(sent_path))
                return true;
            if (TrackerFactory.DefaultTrackerFactory.GetTracker().ActiveDeck == null)
                return true;

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
                        game_started = true;
                        wait_for_prohpecy = false;
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
                            if (currentCard.tempCreated == true)
                            {
                                to_delete.Add(currentCard);
                                continue;
                            }
                            currentCard.resetPlayed();
                            cards.Add(currentCard);
                        }
                        new TriggerChanceUpdater.TriggerChanceUpdater(activeDeck);
                        File.Delete(sent_path);
                        game_started = false;
                        wait_for_prohpecy = false;
                        return true;
                    }

                    bool draw_found = false;
                    draw_from_deck.ForEach(draw_string => draw_found = draw_found || f[i].Contains(draw_string));
                    if (draw_found)
                    {
                        foreach (var draw_string in draw_from_deck)
                        {
                            if (wait_for_prohpecy && f[i].Contains("someone played DefaultLerp card"))
                                wait_for_prohpecy = false;
                            else if (!f[i].Contains(draw_string))
                                continue;

                            int offset = f[i].IndexOf("card=") + ("card=").Length;
                            string played = f[i].Substring(offset);

                            // Mechanic like Elusive Schemer
                            if (draw_string == "player played multiPresent_hand")
                            {
                                CardInstance ToDeck = activeDeck.Where(ci => ci.Card.Name == played).
                                    DefaultIfEmpty(CardInstance.Unknown).FirstOrDefault();
                                if (ToDeck.Card.Name != "Unknown")
                                    ToDeck.decPlayed();
                                else
                                {
                                    Card card = TrackerFactory.DefaultTrackerFactory.GetCardsDatabase().FindCardByName(played);
                                    if (card == Card.Unknown)
                                        break;
                                    ToDeck = new CardInstance(card);
                                    ToDeck.tempCreated = true;
                                    try { activeDeck.Add(ToDeck);} catch { }
                                }

                                cards.Add(ToDeck);
                                break;
                            }
 
                            // only for played prophecy spells
                            if (draw_string == "player played surgeStart_reactionPile")
                            {
                                // don't allow read if that called last one
                                if (i == f.Count() - 1)
                                {
                                    wait_for_prohpecy = true; // and apply prophecy wait reader
                                    File.Delete(sent_path);
                                    return false;
                                }
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
                            }
                            break;
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
        public bool ReadDecks()
        {
            if (Directory.Exists(decks_directory) == false)
                return false;
            bool ret_value = false;
            foreach (string deck_name in Directory.GetFiles(decks_directory))
            {
                string[] f = File.ReadAllLines(deck_name);
                string deck_name_no_path = Path.GetFileNameWithoutExtension(deck_name);
                bool found = false;
                foreach (Deck itr in TrackerFactory.DefaultTrackerFactory.GetTracker().Decks)
                {
                    if (itr.Name == deck_name_no_path)
                    {
                        ImportForDeck(itr);
                        found = true;
                        ret_value = true;
                        break;
                    }
                }
                if (found == false)
                {
                    Deck deck = Deck.CreateNewDeck(deck_name_no_path);
                    deck.Type = deck_name_no_path == "arena" ? DeckType.VersusArena: DeckType.Constructed;
                    ImportForDeck(deck);
                    deck.Class = ClassAttributesHelper.getClassFromCards(deck.SelectedVersion.Cards);
                    TrackerFactory.DefaultTrackerFactory.GetTracker().Decks.Add(deck);
                    ret_value = true;
                }
                File.Delete(deck_name);
            }
            return ret_value;
        }
        private void ImportForDeck(Deck deck)
        {
            DeckImporter deckImporter = new DeckImporter();
            deckImporter.Cards = new List<CardInstance>();
            deckImporter.ImportFromFileProcess(Path.Combine("decks", deck.Name+".txt"));
            deck.SelectedVersion.Cards = new PropertiesObservableCollection<CardInstance>(deckImporter.Cards);
        }

        public bool NeedToModifyDlls()
        {
            if (File.Exists(game_src_lib) == false)
                return false;

            // Using File.ReadLines().First() due to md5 software produces newline
            String md5_orig = File.ReadLines(".\\Resources\\TES-L-Modifided-dll\\md5-orig.txt").First();

            var md5 = MD5.Create();
            var stream = File.OpenRead(game_src_lib);
            String hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
            stream.Close();
 
            if (hash == md5_orig)
                return true;

            return false;
        }
        public Deck UpdateActiveDeck()
        {
            if (File.Exists(deck_selection) == false)
                return null;

            string active_deck_s = File.ReadAllText(deck_selection);
            File.Delete(deck_selection);
            ITracker tracker = TrackerFactory.DefaultTrackerFactory.GetTracker();
            if (tracker.ActiveDeck != null && tracker.ActiveDeck.Name == active_deck_s)
                return null;
            
            foreach (Deck deck in tracker.Decks)
                if (deck.Name == active_deck_s)
                {
                    return deck;
                }
            return null;
        }
        private List<CardInstance> to_delete = new List<CardInstance>();
        public void CleanupActiveDeck()
        {
            var cards = TrackerFactory.DefaultTrackerFactory.GetTracker().ActiveDeck.SelectedVersion.Cards;
            foreach (var card_delete in to_delete)
            {
                cards.Remove(card_delete);
            }
            to_delete.Clear();

        }
    }
}