namespace Oxira.Events.Application.Common;

/// <summary>"Site" section.</summary>
public class SiteOptions
{
    /// <summary>team inbox for leads and bookings</summary>
    public string TeamEmail { get; set; } = "info@oxira.sa";
    public string SiteUrl { get; set; } = "https://events.oxira.sa";
    /// <summary>public address of this API (unsubscribe links)</summary>
    public string ApiUrl { get; set; } = "https://api.oxira.sa/events";
    public string WhatsApp { get; set; } = "+966 56 567 0776";
}

/// <summary>"Ai" section: the website chat agent.</summary>
public class AiOptions
{
    public string Model { get; set; } = "claude-sonnet-5";
    public int MaxTokens { get; set; } = 900;
    public int MaxIterations { get; set; } = 6;
    /// <summary>visible turns kept as memory</summary>
    public int Memory { get; set; } = 14;
    /// <summary>messages per chat session per day</summary>
    public int MessagesPerSession { get; set; } = 60;
    public int MessagesPerDay { get; set; } = 3000;
}

/// <summary>"Renders" section: AI booth renders from the 3D planner.</summary>
public class RenderOptions
{
    public int PerSession { get; set; } = 3;
    public int PerIp { get; set; } = 6;
    public int PerDay { get; set; } = 150;
    /// <summary>largest accepted image (decoded bytes)</summary>
    public int MaxImageBytes { get; set; } = 3_000_000;
}

/// <summary>"Alerts" section: exhibition reminder emails.</summary>
public class AlertOptions
{
    /// <summary>first reminder about two months ahead, second three weeks ahead</summary>
    public int FirstDays { get; set; } = 60;
    public int SecondDays { get; set; } = 21;
    /// <summary>emails are only sent between these Riyadh hours</summary>
    public int FromHour { get; set; } = 9;
    public int ToHour { get; set; } = 21;
}
