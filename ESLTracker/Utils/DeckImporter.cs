﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESLTracker.DataModel;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.IO;

namespace ESLTracker.Utils
{
    public class DeckImporter
    {
        public StringBuilder sbErrors { get; } = new StringBuilder();
        public string Status { get; set; }
        public List<CardInstance> Cards { get; set; }
        public bool DeltaImport { get; set; }

        private TaskCompletionSource<bool> taskCompletonSource;

        private ITrackerFactory trackerFactory;

        public DeckImporter() : this(TrackerFactory.DefaultTrackerFactory)
        {

        }

        public DeckImporter(ITrackerFactory trackerFactory)
        {
            this.trackerFactory = trackerFactory;
        }

        public async Task ImportFromText(string importData)
        {
            Cards = new List<CardInstance>();
            sbErrors.Clear();

            try
            {
                await Task.Run(() => ImportFromTextProcess(importData));

                taskCompletonSource.SetResult(true);
            }
            catch (Exception ex)
            {
                taskCompletonSource.SetException(ex);
            }
        }

        private void ImportFromTextProcess(string importData)
        {
            if (importData == null)
                return;

            foreach (string cardLine in importData.Split(new string[] { Environment.NewLine },
                                                     StringSplitOptions.RemoveEmptyEntries))
            {
                string cardName = cardLine;
                int cardCount = 1;

                Regex regex = new Regex("(|-)[0-9]");
                Match match = regex.Match(cardName);
                if (match.Success)
                {
                    Int32.TryParse(match.Value, out cardCount);
                    cardName = cardLine.Replace(cardCount.ToString(), "");
                }

                Card card = trackerFactory.GetCardsDatabase().FindCardByName(cardName);
                // TODO: Rework for premium
                var found = false;
                foreach (var itr in Cards)
                    if (itr.Card == card)
                    {
                        found = true;
                        itr.Quantity += cardCount;
                    }
                if (!found)
                {
                    CardInstance cardInstance = new CardInstance(card, trackerFactory);
                    cardInstance.Quantity = cardCount;
                    Cards.Add(cardInstance);
                }
            }
            HashSet<CardInstance> cards_silent = new HashSet<CardInstance>() ;
            new TriggerChanceUpdater.TriggerChanceUpdater(new ObservableCollection<CardInstance>(Cards), cards_silent);
            DeckFileReader.DeckFileReader.UpdateGui(cards_silent, false);
        }

        public async Task ImportFromFile()
        {
            Cards = new List<CardInstance>();
            sbErrors.Clear();

            try
            {
                await Task.Run(() => ImportFromFileProcess());
            }
            catch (Exception ex)
            {
                taskCompletonSource.SetException(ex);
            }
        }

        public void ImportFromFileProcess(string path = "deck.txt")
        {
            string full_path = Path.Combine(TrackerFactory.DefaultTrackerFactory.GetTracker().dfr.game_path, path);
            string import_data = "";
            try
            {
                import_data = File.ReadAllText(full_path);
            }
            catch
            {
                return;
            }
            ImportFromTextProcess(import_data);
        }
        internal void CancelImport()
        {
            taskCompletonSource.TrySetResult(false);
        }

        internal void ImportFinished(TaskCompletionSource<bool> tcs)
        {
            taskCompletonSource = tcs;
        }
    }
}
