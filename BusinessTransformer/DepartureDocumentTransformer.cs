﻿using System.Globalization;
using System.Text.RegularExpressions;
using BusinessTransformer.Records;
using CommonInterfaces.DocumentsRelated;
using Departure = BusinessTransformer.Records.Departure;

namespace BusinessTransformer;

/// <summary>
/// A class that transforms a DeparturesDocument into a TrainStation.
/// </summary>
public class DepartureDocumentTransformer : IDocumentTransformer<DeparturesDocument, TrainStation>
{
    private ITimeParser _timeParser = new StandardLibStringManipulator();
    private IStringManipulator _stringManipulator = new StandardLibStringManipulator();

    /// <summary>
    /// Transforms the input (document like structure) to the output object.
    /// </summary>
    /// <param name="input">The input document to transform (already parsed in a business object format, but keeping the general structure of the document).</param>
    /// <returns>The output object transformed.</returns>
    /// <exception cref="FormatException">If the input dates are not in the correct format.</exception> 
    public TrainStation Transform(DeparturesDocument input)
    {
        DateTime documentDate = ParseDate(input.Date);
        string stationName = GetStationNameWithoutPrefix(input.RelatedTrainStationName);
        List<Departure> departures = GetDepartures(stationName, documentDate, input.Departures);
        return new TrainStation(stationName, departures);
    }
    
    /// <summary>
    /// Returns the station name without the prefix.
    /// </summary>
    /// <param name="stationName">The station name to remove the prefix from.</param>
    /// <returns>The station name without the prefix.</returns>
    /// <example>
    /// Bahnhof Zürich Oerlikon -> Zürich Oerlikon
    /// Bahnhof/Station Zürich Flughafen -> Zürich Flughafen
    /// Gare de Yverdon-Champ Pittet -> Yverdon-Champ Pittet
    /// Stazione di Locarno -> Locarno
    /// Gare de Fribourg/Freiburg -> Fribourg/Freiburg
    /// </example>
    private string GetStationNameWithoutPrefix(string stationName)
    {
        string[] prefixes = ["Bahnhof", "Station", "Gare de", "Stazione di"];
        return _stringManipulator.RemovePrefixes(stationName, prefixes, '/');
    }
    
    /// <summary>
    /// Get the business departures for the given station and date.
    /// </summary>
    /// <param name = "stationName">The name of the station to get the departures for.</param>
    /// <param name="date">The date the departures are for.</param>
    /// <param name="departures">The list of departure to parse.</param>
    /// <returns>A list of business departures for the given station and date range ordered by departure time (complete datetime).</returns>
    private List<Departure> GetDepartures(string stationName, DateTime date, List<CommonInterfaces.DocumentsRelated.Departure> departures)
    {
        List<Departure> parsedDepartures = new List<Departure>();
        
        foreach (CommonInterfaces.DocumentsRelated.Departure departure in departures)
        {
            parsedDepartures.Add(GetBusinessDeparture(stationName, date, departure));
        }

        return parsedDepartures.OrderBy(d => d.DepartureTime).ToList();
    }
    
    /// <summary>
    /// Transforms a document departure into a business departure.
    /// Assumes that the departure is valid for the given date.
    /// </summary>
    /// <param name="stationName">The name of the station to get the departures for.</param>
    /// <param name="date">The day of the departure.</param>
    /// <param name="documentDeparture">The document minute departure to transform.</param>
    /// <returns>The business departure transformed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Year is less than 1 or greater than 9999.
    /// month is less than 1 or greater than 12.
    /// day is less than 1 or greater than the number of days in month.
    /// </exception>
    private Departure GetBusinessDeparture(string stationName, DateTime date, CommonInterfaces.DocumentsRelated.Departure documentDeparture)
    {
        (int hour, int minute) = _timeParser.ParseHourMinute(documentDeparture.DepartureHour, " ");
        DateTime departureTime = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0);
        Train train = ParseTrain(documentDeparture.Train);
        List<string> vias = ParseVia(documentDeparture.Via);
        (string platform, string? sector) = ParsePlatform(documentDeparture.Platform);
        return new Departure(stationName, documentDeparture.Destination, vias, departureTime, train, platform, sector);
    }
    
    /// <summary>
    /// Parses the train number and type from the input string.
    /// </summary>
    /// <param name="train">The train number and type in the format IR 15, RE 2, S30, IC1, S1, SN, SN8, RJX, TGV</param>
    /// <returns>The train number and type parsed.</returns>
    /// <example>
    /// IR15 -> IR 15
    /// RE2 -> RE 2
    /// SN -> SN null
    /// </example>
    private Train ParseTrain(string train)
    {
        Regex regex = new Regex(@"(?<g>[A-Z]+)\s*(?<l>\d+)?");
        Match match = regex.Match(train);
        string g = match.Groups["g"].Value;
        string l = match.Groups["l"].Value;
        return new Train(g, _stringManipulator.DoesStringContainsContent(l) ? l : null);
    }

    /// <summary>
    /// Converts a prefixed date string into a DateTime object.
    /// </summary>
    /// <param name="date">A prefixed date string. (Ex. 'Départ pour le 9 décembre 2024' or 'Start am 9 December 2024')</param>
    /// <returns>The parsed date.</returns>
    /// <exception cref="FormatException">Date does not contain a valid string representation of a date and time.</exception>
    private DateTime ParseDate(string date)
    {
        var cultures = new[] { new CultureInfo("fr-FR"), new CultureInfo("de-DE"), new CultureInfo("en-US"), new CultureInfo("it-IT") };
        return _timeParser.ParseLocalisedDate(date, "d MMMM yyyy", cultures);
    }

    
    /// <summary>
    /// Parse the via string into a list of strings. (Separated by a ', ')
    /// </summary>
    /// <param name="via">The via string to parse.</param>
    /// <returns>A list of strings.</returns>
    private List<string> ParseVia(string via)
    {
        return _stringManipulator.Split(via, ", ").ToList();
    }

    /// <summary>
    /// Split sector of the platform.
    /// </summary>
    /// <param name="platform">The platform string to split. Ex. 13D, 1</param>
    /// <returns>A tuple of platform and sector. (13, D) or (1, null)</returns>
    private (string platformOnly, string? sector) ParsePlatform(string platform)
    {
        (string sector, string platformOnly) = _stringManipulator.SplitLetterNumber(platform);
        return (platformOnly, _stringManipulator.DoesStringContainsContent(sector) ? sector : null);
    }
}