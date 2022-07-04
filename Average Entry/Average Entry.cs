using cAlgo.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class AverageEntry : Indicator
    {
        private readonly string TAG = Guid.NewGuid().ToString() + "-AVG";

        #region Parameters
        [Parameter("Show Text ?", DefaultValue = true)]
        public bool ShowText { get; set; }
        #endregion Parameters

        #region Helper Functions
        /// <summary>
        /// Get all opened positions for current Symbol
        /// </summary>
        public Position[] GetSymbolOpenPositions()
        {
            List<Position> openPositions = new List<Position>();

            foreach (Position position in Positions)
                if (position.SymbolName == Symbol.Name)
                    openPositions.Add(position);

            return openPositions.ToArray();
        }

        /// <summary>
        /// Get all opened positions for current Symbol with Trade Type (Buy / Sell)
        /// </summary>
        public Position[] GetSymbolOpenPositions(TradeType tradeType)
        {
            List<Position> openPositions = new List<Position>();

            foreach (Position position in Positions)
                if (position.SymbolName == Symbol.Name && position.TradeType == tradeType)
                    openPositions.Add(position);

            return openPositions.ToArray();
        }

        /// <summary>
        /// Get the Total Volume of Opened Positions
        /// </summary>
        public double GetOpenPositionsVolume() => GetSymbolOpenPositions().Select(p => p.VolumeInUnits).Sum();

        /// <summary>
        /// Get the Total Volume of Opened Positions with Trade Type (Buy / Sell)
        /// </summary>
        public double GetOpenPositionsVolume(TradeType tradeType) => GetSymbolOpenPositions(tradeType).Select(p => p.VolumeInUnits).Sum();

        /// <summary>
        /// Returns the Net Volume Exposure in Units
        /// </summary>
        public double NetVolumeExposure => GetOpenPositionsVolume(TradeType.Buy) - GetOpenPositionsVolume(TradeType.Sell);
        #endregion

        #region Calculate Break-Even
        // Calculate Break-Even Price for Longs or Shorts
        protected void CalculateBreakEven(TradeType tradeType, int index)
        {
            // Skip if no Open Positions
            if (GetSymbolOpenPositions(tradeType).Length == 0)
                return;

            double swap = 0.0;
            double entryVol = 0;

            foreach (Position position in Positions)
            {
                if (position.SymbolName != Symbol.Name || position.TradeType != tradeType)
                    continue;

                int _direction = position.TradeType == TradeType.Sell ? -1 : 1;
                double vol = position.VolumeInUnits * _direction;
                entryVol += position.EntryPrice * vol;
                swap += position.Swap;
            }

            double breakEvenPrice1 = Math.Abs(entryVol / GetOpenPositionsVolume(tradeType));

            CalculateBE(tradeType, breakEvenPrice1, swap, index);
        }

        private void CalculateBE(TradeType? tradeType, double breakEvenPrice1, double swap, int index)
        {
            /* Full Formula:
             * C * (P₂ - P₁) = |S|
             * Where:
             * -- C   = (Volume * Pip Value) / Pip size
             * -- P₁  = Average Entry Before Swaps
             * -- P₂  = Break-Even Price
             * -- |S| = Absolute Swap Value
             */

            double volume = tradeType == null ? NetVolumeExposure : GetOpenPositionsVolume((TradeType)tradeType);
            double constant = (volume * Symbol.PipValue) / Symbol.PipSize;
            double breakEvenPrice2 = Math.Abs(swap / constant) * (tradeType == null ? -1 : 1) + breakEvenPrice1;

            int multipler = ((tradeType == TradeType.Buy && breakEvenPrice2 > Bars.LastBar.Close) || (tradeType == TradeType.Sell && breakEvenPrice2 < Bars.LastBar.Close)) ? -1 : 1;
            double distance = multipler * (Math.Abs(breakEvenPrice2 - Bars.LastBar.Close) / Symbol.PipSize);
            string lineID = "breakEvenPrice";

            Color objCol = Color.Yellow;
            string direction = "AVG";

            if (tradeType != null)
            {
                direction = "AVG " + ((tradeType == TradeType.Buy) ? "L" : "S");
                objCol = (tradeType == TradeType.Buy) ? Color.Green : Color.Red;
                lineID += direction;
            }

            string text = string.Format("{0}: [{1:0.00000} | {2:0.00} Pips]", direction, breakEvenPrice2, distance);
            string textID = lineID + "Text";

            string avgLineName = TAG + lineID;
            Chart.DrawTrendLine(avgLineName, index - 3, breakEvenPrice2, index + 10, breakEvenPrice2, objCol);

            if (!ShowText)
                return;

            string textName = TAG + textID;
            Chart.DrawText(textName, text, index - 2, breakEvenPrice2, objCol);
        }

        private void DrawAverageEntry()
        {
            int index = Bars.ClosePrices.Count - 1;

            bool longsExist = GetSymbolOpenPositions(TradeType.Buy).Length > 0;
            bool shortsExist = GetSymbolOpenPositions(TradeType.Sell).Length > 0;

            if (longsExist)
                CalculateBreakEven(TradeType.Buy, index);

            if (shortsExist)
                CalculateBreakEven(TradeType.Sell, index);
        }
        #endregion

        public override void Calculate(int index) {}
        protected override void Initialize()
        {
            CallEvents();
            Timer.Start(1);
        }

        protected override void OnTimer()
        {
            DrawAverageEntry();
        }

        #region Events
        /// <summary>
        /// Update Average Positions on all Events
        /// </summary>
        public void CallEvents()
        {
            Positions.Opened += OnPositionsOpened;
            Positions.Closed += OnPositionsClosed;
            Positions.Modified += OnPositionsModified;
            Chart.ScrollChanged += OnChartScrollChanged;
            Chart.SizeChanged += OnChartSizeChanged;
            Chart.ZoomChanged += OnChartZoomChanged;
        }

        public void RemoveObjects()
        {
            var cobjects = Chart.Objects.Where(o => o.Name.StartsWith(TAG)).ToList();
            foreach (var cobj in cobjects)
                Chart.RemoveObject(cobj.Name);
        }

        private void OnChartScrollChanged(ChartScrollEventArgs _)
        {
            RemoveObjects();
        }

        private void OnChartSizeChanged(ChartSizeEventArgs _)
        {
            RemoveObjects();
        }

        private void OnChartZoomChanged(ChartZoomEventArgs _)
        {
            RemoveObjects();
        }

        private void OnPositionsClosed(PositionClosedEventArgs _)
        {
            RemoveObjects();
        }

        private void OnPositionsModified(PositionModifiedEventArgs _)
        {
            RemoveObjects();
        }

        private void OnPositionsOpened(PositionOpenedEventArgs _)
        {
            RemoveObjects();
        }

        #endregion Events
    }
}
