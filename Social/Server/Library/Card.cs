﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Library
{
    public class Card
    {
        public int ID;
        public int EnergyCost;
        Effect Effect;

        public void Play()
        {

        }

        public void Discard()
        {

        }
    }

    public enum Effect
    {
        Battlecry, Deathrattle
    }
}
