﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ESLTracker.DataModel.Enums;
using ESLTracker.Properties;
using ESLTracker.Utils;
using ESLTracker.Utils.Messages;
using ESLTracker.Utils.DeckFileReader;

namespace ESLTracker.DataModel
{
    public class Tracker : ViewModels.ViewModelBase, ITracker
    {
        private static Tracker _instance = null;
        public static Tracker Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = TrackerFactory.DefaultTrackerFactory.GetFileManager().LoadDatabase();
                }
                return _instance;
            }
        }

        public DeckFileReader dfr { get; set; } = new DeckFileReader();
        public ObservableCollection<Game> Games { get; set; } = new ObservableCollection<Game>();
        public ObservableCollection<Deck> Decks { get; set; } = new ObservableCollection<Deck>();
        public ObservableCollection<Pack> Packs { get; set; } = new ObservableCollection<Pack>();

        public List<Reward> Rewards { get; set; } = new List<Reward>();

        public SerializableVersion Version { get; set; } = new SerializableVersion(0, 0);
  

        [XmlIgnore]
        public static SerializableVersion CurrentFileVersion = new SerializableVersion(2, 1);

        // binding!!!

        //Deck selected in applications
        private Deck activeDeck;
        [XmlIgnore]
        public Deck ActiveDeck
        {
            get
            {
                return activeDeck;
            }
            set
            {
                activeDeck = value;
                trackerFactory.GetMessanger().Send(new ActiveDeckChanged(value));
                UpdateActiveDeck();
            }
        }
        public void UpdateActiveDeck()
        {
            RaisePropertyChangedEvent(nameof(ActiveDeck));
        }

        private ITrackerFactory trackerFactory;
        public Tracker() : this(TrackerFactory.DefaultTrackerFactory)
        {

        }

        public Tracker(ITrackerFactory trackerFactory)
        {
            this.trackerFactory = trackerFactory;
        }

        public IEnumerable<Reward> GetRewardsSummaryByType(RewardType type)
        {
            return Instance.Rewards.Where(r => r.Type == type);
        }

    }
}
