using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Harmony;
using Verse;
using RimWorld;

namespace RangedStaggerRebalanced
{
    public class StoppingPowerExtension : DefModExtension
    {

        public static readonly StoppingPowerExtension defaultValues = new StoppingPowerExtension();

        public float stoppingPowerFactor = 1f;

    }
}
