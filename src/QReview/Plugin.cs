using System;
using System.IO;
using System.Text;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.Results;

namespace QReview
{
    public class Plugin : IFieldDataPlugin
    {
        static Plugin()
        {
            // Included to avoid "'Windows-1252' is not a supported encoding name" error in net10
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public ParseFileResult ParseFile(Stream fileStream, IFieldDataResultsAppender appender, ILog logger)
        {
            return ParseFile(fileStream, null, appender, logger);
        }

        private Config Config { get; set; }

        public ParseFileResult ParseFile(Stream fileStream, LocationInfo targetLocation, IFieldDataResultsAppender appender, ILog logger)
        {
            try
            {
                Config = new ConfigLoader(appender)
                    .Load();

                var summary = GetSummary(fileStream, logger);

                if (summary == null)
                    return ParseFileResult.CannotParse();

                if (targetLocation == null)
                {
                    var locationIdentifier = ExtractLocationIdentifier(summary.StationName);

                    if (string.IsNullOrEmpty(locationIdentifier))
                        return ParseFileResult.SuccessfullyParsedButDataInvalid("Missing station name");

                    targetLocation = appender.GetLocationByIdentifier(locationIdentifier);
                }

                var parser = new Parser(Config, targetLocation, appender, logger);

                parser.Parse(summary);

                return ParseFileResult.SuccessfullyParsedAndDataValid();
            }
            catch (Exception e)
            {
                return ParseFileResult.SuccessfullyParsedButDataInvalid(e.Message);
            }
        }

        private string ExtractLocationIdentifier(string stationName)
        {
            var locationIdentifier = stationName;

            if (string.IsNullOrEmpty(locationIdentifier))
                return locationIdentifier;

            if (!string.IsNullOrEmpty(Config.LocationIdentifierSeparator))
            {
                locationIdentifier = locationIdentifier
                    .Split(new[] {Config.LocationIdentifierSeparator}, StringSplitOptions.None)
                    [0];
            }

            if (!int.TryParse(locationIdentifier, out var numericValue))
                return locationIdentifier;

            if (Config.LocationIdentifierZeroPaddedDigits < 1)
                return locationIdentifier;

            return numericValue.ToString($"D{Config.LocationIdentifierZeroPaddedDigits}");
        }

        private DischargeMeasurementSummary GetSummary(Stream fileStream, ILog logger)
        {
            try
            {
                return new TsvParser(Config)
                    .Load(fileStream);
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return null;
            }
        }
    }
}
