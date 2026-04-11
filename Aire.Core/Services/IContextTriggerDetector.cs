using System.Collections.Generic;
using Aire.Data;

namespace Aire.Services
{
    /// <summary>
    /// Represents the result of analyzing recent messages for contextual triggers.
    /// </summary>
    public readonly struct ContextTriggerDetection
    {
        /// <summary>
        /// Gets a value indicating whether the conversation is currently focused on tool usage.
        /// </summary>
        public bool IsToolFocus { get; init; }

        /// <summary>
        /// Gets a value indicating whether the latest message is a retry or follow-up request.
        /// </summary>
        public bool IsRetryFollowUp { get; init; }

        /// <summary>
        /// Creates a new instance of <see cref="ContextTriggerDetection"/>.
        /// </summary>
        public ContextTriggerDetection(bool isToolFocus, bool isRetryFollowUp)
        {
            IsToolFocus = isToolFocus;
            IsRetryFollowUp = isRetryFollowUp;
        }

        /// <summary>
        /// Returns an empty detection (no triggers).
        /// </summary>
        public static ContextTriggerDetection None => new(false, false);
    }

    /// <summary>
    /// Analyzes recent messages to detect contextual triggers that influence conversation compaction.
    /// </summary>
    public interface IContextTriggerDetector
    {
        /// <summary>
        /// Examines the given messages and returns detected triggers.
        /// </summary>
        /// <param name="recentMessages">The most recent messages in the conversation (chronological order).</param>
        /// <returns>A <see cref="ContextTriggerDetection"/> instance describing the detected triggers.</returns>
        ContextTriggerDetection DetectTriggers(IReadOnlyList<Message> recentMessages);
    }
}