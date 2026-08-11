using BuildingBlocks.Application.Abstractions;
using System;
using System.IO;

namespace Identity.Application.Commands;

public sealed record UploadAvatarCommand(Guid UserId, Stream Content, string FileName, string ContentType) : ICommand<string>;
