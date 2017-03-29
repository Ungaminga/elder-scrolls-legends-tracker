using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESLTracker.DataModel;

namespace ESLTracker.Utils.Messages
{
    class ActiveDeckChanged
    {
        public Deck ActiveDeck { get; set; }

        public ActiveDeckChanged(Deck activeDeck)
        {
            this.ActiveDeck = activeDeck;

            HashSet<CardInstance> cards_silent = new HashSet<CardInstance>();
            new TriggerChanceUpdater.TriggerChanceUpdater(activeDeck.SelectedVersion.Cards, cards_silent);
            DeckFileReader.DeckFileReader.UpdateGui(cards_silent, false);
        }
    }
}
