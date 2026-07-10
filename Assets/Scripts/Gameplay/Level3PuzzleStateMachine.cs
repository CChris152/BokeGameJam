using System;

namespace BokeGameJam.Gameplay
{
    public enum MirrorShardColor
    {
        Yellow,
        Red,
        Green
    }

    public enum Level3PuzzleStage
    {
        Locked,
        ShardSequence,
        MirrorAssembly,
        Completed
    }

    public enum MirrorShardDeliveryResult
    {
        Unavailable,
        Accepted,
        WrongOrder,
        SequenceCompleted
    }

    /// <summary>
    /// Pure gameplay state for Level 3. Presentation and scene objects react to
    /// this state but do not own password, sequence, or completion rules.
    /// </summary>
    [Serializable]
    public sealed class Level3PuzzleStateMachine
    {
        private static readonly MirrorShardColor[] RequiredOrder =
        {
            MirrorShardColor.Yellow,
            MirrorShardColor.Red,
            MirrorShardColor.Green
        };

        private Level3PuzzleStage stage = Level3PuzzleStage.Locked;
        private int deliveredCount;

        public Level3PuzzleStage Stage => stage;
        public int DeliveredCount => deliveredCount;
        public bool CanCollectShards => stage == Level3PuzzleStage.ShardSequence;
        public MirrorShardColor? ExpectedColor =>
            stage == Level3PuzzleStage.ShardSequence
                ? RequiredOrder[deliveredCount]
                : null;

        public bool Unlock()
        {
            if (stage != Level3PuzzleStage.Locked)
                return false;

            deliveredCount = 0;
            stage = Level3PuzzleStage.ShardSequence;
            return true;
        }

        public MirrorShardDeliveryResult Deliver(MirrorShardColor color)
        {
            if (stage != Level3PuzzleStage.ShardSequence)
                return MirrorShardDeliveryResult.Unavailable;

            if (color != RequiredOrder[deliveredCount])
            {
                deliveredCount = 0;
                return MirrorShardDeliveryResult.WrongOrder;
            }

            deliveredCount++;
            if (deliveredCount < RequiredOrder.Length)
                return MirrorShardDeliveryResult.Accepted;

            stage = Level3PuzzleStage.MirrorAssembly;
            return MirrorShardDeliveryResult.SequenceCompleted;
        }

        public bool CompleteMirrorAssembly()
        {
            if (stage != Level3PuzzleStage.MirrorAssembly)
                return false;

            stage = Level3PuzzleStage.Completed;
            return true;
        }

        public void Reset()
        {
            deliveredCount = 0;
            stage = Level3PuzzleStage.Locked;
        }
    }
}
