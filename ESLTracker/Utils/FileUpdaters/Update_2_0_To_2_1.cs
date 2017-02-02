﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ESLTracker.DataModel;

namespace ESLTracker.Utils.FileUpdaters
{
    [SerializableVersion(2, 0)]
    public class Update_2_0_To_2_1 : UpdateBase
    {
        public override SerializableVersion TargetVersion { get; } = new SerializableVersion(2, 1);

        protected override void VersionSpecificUpdateFile(XmlDocument doc, Tracker tracker)
        {
            //do nothing
            //deck.ishidden is false by defualt (boolean)
            SetDeckLastUsed(tracker);
        }

        public void SetDeckLastUsed(ITracker tracker)
        {
            foreach(Deck d in tracker.Decks)
            {
                Game lastGame = tracker.Games.Where(g => g.DeckId == d.DeckId).OrderByDescending(g => g.Date).FirstOrDefault();
                if (lastGame != null)
                {
                    d.LastUsed = lastGame.Date;
                }
                else
                {
                    d.LastUsed = d.CreatedDate;
                }
            }
        }
    }
}
