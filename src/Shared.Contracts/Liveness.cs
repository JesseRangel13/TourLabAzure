namespace TourLab.Azure.Contracts;

// Transport data only. No business models or persistence are shared.
public sealed record Liveness(string Service, string Status, string Stage);
