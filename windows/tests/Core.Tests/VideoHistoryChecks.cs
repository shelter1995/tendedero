using Liangtu.Core;
static class VideoHistoryChecks
{
    public static void Run(Action<string,Action> test)
    {
        void Assert(bool ok) { if(!ok) throw new Exception("Selection history mismatch"); }
        test("video history restores both endpoints and reset can be undone",()=>{
            var h=new VideoSelectionHistory(new(0,12)); h.Commit(new(2.5,7.5)); h.Commit(new(0,12));
            Assert(h.Undo() && h.Current==new VideoSelection(2.5,7.5)); Assert(h.Undo() && h.Current==new VideoSelection(0,12));
            Assert(!h.Undo() && h.Redo() && h.Current==new VideoSelection(2.5,7.5));
        });
        test("video no-op preserves redo and new edits discard redo",()=>{
            var h=new VideoSelectionHistory(new(0,12));h.Commit(new(2,10));h.Undo();h.Commit(new(0,12));Assert(h.CanRedo);
            h.Commit(new(3,11));Assert(!h.CanRedo && h.CanUndo);
        });
        test("video history keeps bounded valid revisions",()=>{
            var h=new VideoSelectionHistory(new(0,12));for(var i=1;i<=250;i++)h.Commit(new(i/100.0,12));
            var count=0;while(h.Undo())count++;Assert(count==99);while(h.Redo()){}Assert(h.Current==new VideoSelection(2.5,12));
        });
    }
}
