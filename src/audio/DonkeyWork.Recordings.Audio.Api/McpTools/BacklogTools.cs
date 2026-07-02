using System.ComponentModel;
using DonkeyWork.Recordings.Audio.Contracts.Models;
using DonkeyWork.Recordings.Audio.Contracts.Services;
using DonkeyWork.Recordings.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace DonkeyWork.Recordings.Audio.Api.McpTools;

[McpServerToolType]
[McpToolProvider(Provider = McpToolProvider.DonkeyWork)]
public class BacklogTools
{
    private readonly IBacklogService _backlogService;

    public BacklogTools(IBacklogService backlogService)
    {
        _backlogService = backlogService;
    }

    [McpServerTool(Name = "list_backlog_items", Title = "List Backlog Items", ReadOnly = true)]
    [Description(
        "List content backlog items for a channel. Defaults to Pending only — the queue of stories, links, and " +
        "notes waiting to be worked into the channel's next episode. Episode generators should call this BEFORE " +
        "writing a script, weave the pending items into the narrative, then call consume_backlog_items with the " +
        "ids they used and the new recording's id. Pass status Consumed or Dismissed to browse history, or 'all' " +
        "for everything. Returns null if the channel does not exist.")]
    public Task<ListBacklogItemsResponseV1?> ListBacklogItems(
        [Description("The channel (collection) id whose backlog to list.")] Guid collectionId,
        [Description("Status filter: Pending (default), Consumed, Dismissed, or 'all'.")] string? status = "Pending",
        [Description("Pagination offset (default 0).")] int? offset = null,
        [Description("Page size (default 50, max 200).")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return _backlogService.ListAsync(collectionId, status, offset ?? 0, limit ?? 50, cancellationToken);
    }

    [McpServerTool(Name = "add_backlog_item", Title = "Add Backlog Item")]
    [Description(
        "Add a content item to a channel's backlog — a story, link, or note to be covered in a future episode of " +
        "that channel. Items stay Pending until an episode generator consumes them (consume_backlog_items) or they " +
        "are dismissed. Returns null if the channel does not exist.")]
    public Task<BacklogItemV1?> AddBacklogItem(
        [Description("The channel (collection) id the item belongs to.")] Guid collectionId,
        [Description("Short headline for the item.")] string title,
        [Description("The content/body to incorporate into the episode (markdown or plain text).")] string? content = null,
        [Description("Optional source URL for attribution or follow-up.")] string? sourceUrl = null,
        [Description("Optional editorial guidance for the generator, e.g. 'mention briefly at the end'.")] string? notes = null,
        CancellationToken cancellationToken = default)
    {
        return _backlogService.CreateAsync(collectionId, new CreateBacklogItemRequestV1
        {
            Title = title,
            Content = content,
            SourceUrl = sourceUrl,
            Notes = notes,
        }, cancellationToken);
    }

    [McpServerTool(Name = "update_backlog_item", Title = "Update Backlog Item")]
    [Description(
        "Patch a backlog item's title, content, source URL, or notes. Omit fields to keep them unchanged. Does not " +
        "change status — use consume_backlog_items or dismiss_backlog_item for that.")]
    public Task<BacklogItemV1?> UpdateBacklogItem(
        [Description("The backlog item id.")] Guid id,
        [Description("New title (omit to keep current).")] string? title = null,
        [Description("New content (omit to keep current).")] string? content = null,
        [Description("New source URL (omit to keep current).")] string? sourceUrl = null,
        [Description("New notes (omit to keep current).")] string? notes = null,
        CancellationToken cancellationToken = default)
    {
        return _backlogService.UpdateAsync(id, new UpdateBacklogItemRequestV1
        {
            Title = title,
            Content = content,
            SourceUrl = sourceUrl,
            Notes = notes,
        }, cancellationToken);
    }

    [McpServerTool(Name = "consume_backlog_items", Title = "Consume Backlog Items")]
    [Description(
        "Mark backlog items as Consumed by an episode, keeping them as history linked to that recording. Call this " +
        "AFTER creating the recording that incorporated them. Pass the exact itemIds that were used (recommended), " +
        "or omit itemIds to consume every currently-pending item in the channel. Idempotent: items already consumed " +
        "or dismissed are reported in SkippedIds and keep their original episode link. Returns null if the channel " +
        "or recording does not exist.")]
    public Task<ConsumeBacklogItemsResponseV1?> ConsumeBacklogItems(
        [Description("The channel (collection) id.")] Guid collectionId,
        [Description("The recording (episode) id that incorporated the items.")] Guid recordingId,
        [Description("The backlog item ids that were used. Omit to consume all pending items in the channel.")] Guid[]? itemIds = null,
        CancellationToken cancellationToken = default)
    {
        return _backlogService.ConsumeAsync(collectionId, new ConsumeBacklogItemsRequestV1
        {
            RecordingId = recordingId,
            ItemIds = itemIds,
        }, cancellationToken);
    }

    [McpServerTool(Name = "dismiss_backlog_item", Title = "Dismiss Backlog Item")]
    [Description(
        "Dismiss a pending backlog item without using it (e.g. stale or superseded). Kept as history with status " +
        "Dismissed. Idempotent — dismissing a non-pending item returns it unchanged.")]
    public Task<BacklogItemV1?> DismissBacklogItem(
        [Description("The backlog item id.")] Guid id,
        CancellationToken cancellationToken = default)
    {
        return _backlogService.DismissAsync(id, cancellationToken);
    }

    [McpServerTool(Name = "delete_backlog_item", Title = "Delete Backlog Item")]
    [Description(
        "Permanently delete a backlog item, including its consumption history. Prefer dismiss_backlog_item when the " +
        "item should be kept as history.")]
    public Task<bool> DeleteBacklogItem(
        [Description("The backlog item id.")] Guid id,
        CancellationToken cancellationToken = default)
    {
        return _backlogService.DeleteAsync(id, cancellationToken);
    }
}
