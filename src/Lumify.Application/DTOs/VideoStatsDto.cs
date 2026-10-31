namespace Lumify.Application.DTOs;

public record VideoStatsDto(Guid VideoId, long Views, long Likes, bool LikedByMe);
