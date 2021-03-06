using System;
using System.Text.RegularExpressions;

namespace Drako.Api.Controllers
{
    public class BettingOdds
    { 
        private static readonly Regex OddsMatcher = new(@"^(?:\[(?<numerator>\d+):(?<denominator>\d+)\])?$");
        
        public BettingOdds(string odds)
        {
            if (odds == null)
            {
                Numerator = -1;
                Denominator = 0;
            }
            else
            {
                var match = OddsMatcher.Match(odds);
                Numerator = match.Groups["numerator"].Success ? int.Parse(match.Groups["numerator"].Value) : 0;
                Denominator = match.Groups["numerator"].Success ? int.Parse(match.Groups["denominator"].Value) : 1;
                Denominator = Math.Max(1, Denominator);
            }
        }
        
        public int Numerator { get; }
        public int Denominator { get; }

        public decimal WinMultiplier(long totalBets, long sum)
        {
            if (Numerator == -1)
            {
                if (sum == 0)
                {
                    return 0m;
                }
                return Convert.ToDecimal(totalBets) / Convert.ToDecimal(sum);
            }

            if (Denominator == 0)
            {
                return 0m;
            }
            return Convert.ToDecimal(Numerator) / Convert.ToDecimal(Denominator);
        }
    }
}