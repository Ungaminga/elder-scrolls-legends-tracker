using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ESLTracker.DataModel.Enums;
using ESLTracker.Utils.DeckFileReader;

namespace ESLTracker.DataModel
{
    public interface ITracker
    {
        Deck ActiveDeck { get; set; }
        ObservableCollection<Deck> Decks { get; set; }
        ObservableCollection<Game> Games { get; set; }
        List<Reward> Rewards { get; set; }
        ObservableCollection<Pack> Packs { get; set; }

        DeckFileReader dfr { get; set; }

        IEnumerable<Reward> GetRewardsSummaryByType(RewardType type);
    }
}