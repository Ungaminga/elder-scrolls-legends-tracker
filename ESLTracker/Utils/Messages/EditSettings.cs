﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESLTracker.DataModel;
using ESLTracker.ViewModels.Decks;

namespace ESLTracker.Utils.Messages
{
    public class EditSettings
    {
        public enum Context
        {
            StartEdit,
            EditFinished
        }
    }
}
