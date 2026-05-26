using Infrastructure.Watermark;

namespace Infrastructure.Scheduler;

public record WatermarkContext(DateTimeOffset Watermark, DateTimeOffset Now);
