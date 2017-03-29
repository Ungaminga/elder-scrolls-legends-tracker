using ESLTracker.DataModel;
using ESLTracker.DataModel.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ESLTracker.Utils.TriggerChanceUpdater
{
    class TriggerChanceUpdater
    {
        public TriggerChanceUpdater(ObservableCollection<CardInstance> cards, HashSet<CardInstance> to_update=null)
        {
            int total = 0;
            foreach (var itr in cards)
                total += itr.Least;

            foreach (var itr in cards)
            {
                switch (itr.Card.TriggerType)
                {
                    case "ally":
                        {
                            DeckAttribute color = itr.Card.Attributes.First();
                            int count = 0;
                            foreach (var jtr in cards)
                            {
                                if (jtr.Card.Attributes.Contains(color))
                                    count += jtr.Least;
                            }
                            itr.TriggerChance = Math.Round(count * 100.0f / total, 2).ToString();
                            if (to_update != null)
                                to_update.Add(itr);
                            break;
                        }
                    case "prophecy":
                        {
                            itr.TriggerChance = Math.Round(itr.Least * 100.0f / total, 2).ToString();
                            if (to_update != null)
                                to_update.Add(itr);
                            break;
                        }
                    case "action_draw":
                        {
                            int count = 0;
                            foreach (var jtr in cards)
                                if (jtr.Card.Type == CardType.Action)
                                    count += jtr.Least;

                            itr.TriggerChance = Math.Round(count * 100.0f / total, 2).ToString();
                            break;
                        }
                    default: break;
                }
            }
        }
    }
}