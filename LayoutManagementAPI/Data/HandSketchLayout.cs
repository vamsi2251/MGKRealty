using System.Text.Json;
using LayoutManagementAPI.Contracts;
using LayoutManagementAPI.Models;

namespace LayoutManagementAPI.Data;

public static class HandSketchLayout
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static LayoutPlan Create(string fileName, string uploadedBy)
    {
        var plots = new List<Plot>
        {
            Rectangle("1", 0, 300, 30, 40, 2.75m, "North"),
            Rectangle("2", 0, 240, 40, 60, 5.51m, "East"),
            Rectangle("3", 0, 180, 40, 60, 5.51m, "East"),
            Rectangle("4", 0, 120, 40, 60, 5.51m, "East"),
            Rectangle("5", 0, 60, 40, 60, 5.51m, "East"),
            Rectangle("6", 0, 0, 40, 60, 5.51m, "East"),
            Rectangle("7", 70, 290, 60, 50, 6.89m, "West"),
            Rectangle("8", 70, 245, 40, 45, 4.13m, "West"),
            Rectangle("9", 70, 200, 40, 45, 4.13m, "West"),
            Rectangle("10", 70, 134, 50, 66, 7.58m, "West")
        };

        return new LayoutPlan
        {
            Name = "Layout sketch",
            Unit = "cent",
            Notes = "Split from the uploaded sketch. Left of the 30 foot road: plot 1 is 30' x 40' (2.75 cents) and plots 2-6 are 40' x 60' (5.51 cents). Right of the road: plot 7 is 60' x 50' (6.89 cents), plots 8 and 9 are 40' x 45' (4.13 cents), and plot 10 is 50' x 66' (7.58 cents). One cent is 435.6 square feet. Update a plot area if the written value needs a correction.",
            FileName = fileName,
            UploadedBy = uploadedBy,
            UploadedOn = DateTime.UtcNow,
            Plots = plots
        };
    }

    private static Plot Rectangle(string number, decimal x, decimal y, decimal breadthFeet, decimal lengthFeet, decimal cents, string facing)
    {
        var vertices = new[]
        {
            new SketchPoint { X = x, Y = y },
            new SketchPoint { X = x + breadthFeet, Y = y },
            new SketchPoint { X = x + breadthFeet, Y = y + lengthFeet },
            new SketchPoint { X = x, Y = y + lengthFeet }
        };

        return new Plot
        {
            PlotNumber = number,
            Area = cents,
            BreadthFeet = breadthFeet,
            LengthFeet = lengthFeet,
            Facing = facing,
            Status = PlotStatus.Available,
            VerticesJson = JsonSerializer.Serialize(vertices, JsonOptions)
        };
    }
}
