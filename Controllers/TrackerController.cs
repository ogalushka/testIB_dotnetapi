using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.RegularExpressions;
using tracker.Dto;
using tracker.Repository;
using tracker.Repository.Entity;
using tracker.Viber;
using tracker.Viber.dto;

namespace tracker.Controllers;

[ApiController]
public class TrackerController : ControllerBase
{
    private static TimeSpan StrollTimeout = TimeSpan.FromMinutes(30);

    private readonly TrackRepository repository;
    private readonly ViberHttpClient viberClient;

    public TrackerController(TrackRepository repository, ViberHttpClient viberClient)
    {
        this.repository = repository;
        this.viberClient = viberClient;
    }

    [HttpPost]
    [Route("/")]
    public async Task<IActionResult> Viber([FromBody] ViberEvent e)
    {
        if (e.Event == ViberEventType.ConversationStarted)
        {
            return Ok(new ViberSendMessage(ViberMessageType.Text, "Enter IMEI"));
        }

        if (e.Event == ViberEventType.Message && e.Message != null && !string.IsNullOrWhiteSpace(e.Message.text))
        {
            var messageText = e.Message.text.Trim();
            var isTopRequest = e.Message.text.StartsWith("/top ");
            if (isTopRequest)
            {
                var imei = messageText.Substring("/top ".Length);
                await ShowTop(e.Sender.id, imei);
            }
            else
            {
                await ShowStats(e.Sender.id, e.Message.text);
            }
        }

        return Ok();
    }

    private async Task ShowStats(string senderId, string imei)
    {
        if (!isValidImei(imei))
        {
            await viberClient.SendText(senderId, "Please enter a valid IMEI");
            return;
        }

        var stats = await GetStats(imei);
        if (stats == null)
        {
            await viberClient.SendText(senderId, $"No strolls found for IMEI {imei}");
        }
        else
        {
            var roundedDistance = Math.Round(stats.distance * 1000) / 1000;
            var roundedMinutes = Math.Round(stats.duration.TotalMinutes);
            var output = $"Total strolls: {stats.count}\nTotal distance km: {roundedDistance}\nTotal duration min: {roundedMinutes}";
            await viberClient.SendButton(senderId, output, "Top 10", $"/top {imei}");
        }
    }

    private async Task ShowTop(string senderId, string imei)
    {
        if (!isValidImei(imei))
        {
            await viberClient.SendText(senderId, "Please enter a valid IMEI");
            return;
        }

        var strolls = await GetTopStrolls(imei);
        if (strolls.Length == 0)
        {
            await viberClient.SendText(senderId, $"No strolls found for IMEI:{imei}");
            return;
        }

        await viberClient.SendButton(senderId, FormatTop(strolls), "Back", imei);
    }

    private bool isValidImei(string imei)
    {
        return new Regex(@"^[0-9]{15}").IsMatch(imei);
    }

    private string FormatTop(StrollDto[] strolls)
    {
        var indexTitle = " N";
        var distanceTitle = "      km";
        var durationTitle = "     min";
        var indexSize = indexTitle.Length;
        var distanceLength = distanceTitle.Length;
        var durationLength = durationTitle.Length;

        var responseBuilder = new StringBuilder($"```|{indexTitle}|{distanceTitle}|{durationTitle}|");
        for (var i = 0; i < strolls.Length; i++)
        {
            var stroll = strolls[i];
            var distance = Math.Round(stroll.DistanceKm * 1000) / 1000;
            var duration = Math.Round(stroll.Duration.TotalMinutes);

            responseBuilder.Append('\n');
            responseBuilder.Append('|');
            responseBuilder.Append((i + 1).ToString().PadLeft(indexSize));
            responseBuilder.Append('|');
            responseBuilder.Append(distance.ToString().PadLeft(distanceLength));
            responseBuilder.Append('|');
            responseBuilder.Append(duration.ToString().PadLeft(durationLength));
            responseBuilder.Append('|');
        }
        responseBuilder.Append("```");
        return responseBuilder.ToString();
    }

    private async Task<StatsDto?> GetStats(string imei)
    {
        var trackingPoints = repository.GetTracks(imei);
        var strolls = await ParseStrolls(trackingPoints);
        if (strolls.Count() == 0)
        {
            return null;
        }

        var totalDistance = .0;
        var totalDuration = TimeSpan.Zero;
        foreach (var stroll in strolls)
        {
            totalDistance += stroll.DistanceKm;
            totalDuration += stroll.Duration;
        }
        return new StatsDto(totalDistance, totalDuration, strolls.Count());
    }

    private async Task<StrollDto[]> GetTopStrolls(string imei)
    {
        var trackingPoints = repository.GetTracks(imei);
        var strolls = await ParseStrolls(trackingPoints);
        return strolls.OrderByDescending(t => t.DistanceKm).Take(10).ToArray();
    }

    private async Task<IEnumerable<StrollDto>> ParseStrolls(IAsyncEnumerator<TrackRecord> records)
    {
        var strolls = new List<StrollDto>();
        TrackRecord? previousRecord = null;
        StrollDto currentStroll = new StrollDto();
        while (await records.MoveNextAsync())
        {
            var record = records.Current;
            if (previousRecord == null)
            {
                previousRecord = record;
                continue;
            }

            var deltaTime = record.date_track - previousRecord.date_track;
            var deltaDistance = GetCoordinatiesDistanceInKm(record.latitude, record.longitude, previousRecord.latitude, previousRecord.longitude);

            if (deltaTime < StrollTimeout)
            {
                currentStroll.Duration += deltaTime;
                currentStroll.DistanceKm += deltaDistance;
            }
            else 
            {
                if (currentStroll.DistanceKm > double.Epsilon || currentStroll.Duration > TimeSpan.Zero)
                {
                    strolls.Add(currentStroll);
                    currentStroll = new StrollDto();
                }
            }

            previousRecord = record;
        }
        return strolls;
    }

    private double GetCoordinatiesDistanceInKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        var toRads = Math.PI / 180;
        var lat1Rads = (double)lat1 * toRads;
        var lat2Rads = (double)lat2 * toRads;
        var lon1Rads = (double)lon1 * toRads;
        var lon2Rads = (double)lon2 * toRads;
        var earthRadiusKm = 6371; 
        var dLat = lat2Rads - lat1Rads;
        var dLon = lon2Rads - lon1Rads;

        var a =
          Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
          Math.Cos(lat1Rads) * Math.Cos(lat2Rads) *
          Math.Sin(dLon / 2) * Math.Sin(dLon / 2)
          ;
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var d = earthRadiusKm * c;
        return d;
    }
}
