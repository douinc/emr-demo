using EmrDemo.Core;

namespace EmrDemo.Core.Tests;

public class TextBufferTests
{
    [Fact]
    public void New_PlacesCaretAtEnd()
    {
        var buffer = new TextBuffer("가나");

        Assert.Equal(2, buffer.Caret);
        Assert.False(buffer.HasSelection);
    }

    [Fact]
    public void Insert_AtCaret()
    {
        var buffer = new TextBuffer("가다");
        buffer.MoveTo(1);

        buffer.Insert("나");

        Assert.Equal("가나다", buffer.Text);
        Assert.Equal(2, buffer.Caret);
    }

    [Fact]
    public void Insert_ReplacesSelection()
    {
        var buffer = new TextBuffer("원래 기록");
        buffer.SelectAll();

        buffer.Insert("새록 요약");

        Assert.Equal("새록 요약", buffer.Text);
        Assert.Equal(5, buffer.Caret);
        Assert.False(buffer.HasSelection);
    }

    [Fact]
    public void Insert_NormalizesWindowsNewlines()
    {
        var buffer = new TextBuffer("");

        buffer.Insert("S\r\nO\rA\nP");

        Assert.Equal("S\nO\nA\nP", buffer.Text);
    }

    [Fact]
    public void SingleLine_Insert_ReplacesNewlinesWithSpace()
    {
        var buffer = new TextBuffer("", singleLine: true);

        buffer.Insert("알레지온\r\n점안액\n");

        Assert.Equal("알레지온 점안액 ", buffer.Text);
    }

    [Fact]
    public void SingleLine_Constructor_ReplacesNewlines()
    {
        Assert.Equal("a b", new TextBuffer("a\nb", singleLine: true).Text);
    }

    [Fact]
    public void Backspace_DeletesBeforeCaret_OrSelection()
    {
        var buffer = new TextBuffer("가나다");
        buffer.Backspace();
        Assert.Equal("가나", buffer.Text);

        buffer.MoveTo(0);
        buffer.Backspace();
        Assert.Equal("가나", buffer.Text);

        buffer.MoveTo(0);
        buffer.MoveTo(2, extend: true);
        buffer.Backspace();
        Assert.Equal("", buffer.Text);
        Assert.Equal(0, buffer.Caret);
    }

    [Fact]
    public void Delete_DeletesAfterCaret()
    {
        var buffer = new TextBuffer("가나다");
        buffer.MoveTo(1);

        buffer.Delete();

        Assert.Equal("가다", buffer.Text);
        Assert.Equal(1, buffer.Caret);

        buffer.MoveTo(2);
        buffer.Delete();
        Assert.Equal("가다", buffer.Text);
    }

    [Fact]
    public void MoveLeftRight_ClampsAndCollapsesSelection()
    {
        var buffer = new TextBuffer("가나");
        buffer.MoveRight();
        Assert.Equal(2, buffer.Caret);

        buffer.SelectAll();
        buffer.MoveLeft();
        Assert.Equal(0, buffer.Caret);
        Assert.False(buffer.HasSelection);

        buffer.MoveLeft();
        Assert.Equal(0, buffer.Caret);

        buffer.MoveRight(extend: true);
        Assert.Equal("가", buffer.SelectedText);
    }

    [Fact]
    public void Moves_DoNotSplitSurrogatePairs()
    {
        var buffer = new TextBuffer("a😀b");
        buffer.MoveTo(1);

        buffer.MoveRight();
        Assert.Equal(3, buffer.Caret);

        buffer.Backspace();
        Assert.Equal("ab", buffer.Text);
        Assert.Equal(1, buffer.Caret);

        buffer.Insert("😀");
        buffer.MoveTo(1);
        buffer.Delete();
        Assert.Equal("ab", buffer.Text);
    }

    [Fact]
    public void HomeEnd_MoveWithinCurrentLine()
    {
        var buffer = new TextBuffer("S\n최근 검사\nO");
        buffer.MoveTo(4);

        buffer.MoveLineStart();
        Assert.Equal(2, buffer.Caret);

        buffer.MoveLineEnd();
        Assert.Equal(7, buffer.Caret);
    }

    [Fact]
    public void MoveTo_ClampsOutOfRange()
    {
        var buffer = new TextBuffer("가나");

        buffer.MoveTo(-3);
        Assert.Equal(0, buffer.Caret);

        buffer.MoveTo(99);
        Assert.Equal(2, buffer.Caret);
    }

    [Fact]
    public void SelectionRange_IsOrderedRegardlessOfDirection()
    {
        var buffer = new TextBuffer("가나다라");
        buffer.MoveTo(3);
        buffer.MoveTo(1, extend: true);

        Assert.Equal((1, 3), buffer.Selection);
        Assert.Equal("나다", buffer.SelectedText);
    }

    [Fact]
    public void SetText_ResetsCaretToEnd()
    {
        var buffer = new TextBuffer("가");
        buffer.SelectAll();

        buffer.SetText("나다");

        Assert.Equal("나다", buffer.Text);
        Assert.Equal(2, buffer.Caret);
        Assert.False(buffer.HasSelection);
    }

    [Fact]
    public void MoveTo_InsideSurrogatePair_SnapsToPairStart()
    {
        var buffer = new TextBuffer("a😀b");

        buffer.MoveTo(2);
        Assert.Equal(1, buffer.Caret);

        buffer.MoveTo(3);
        buffer.MoveTo(2, extend: true);
        Assert.Equal("😀", buffer.SelectedText);

        buffer.MoveTo(3);
        buffer.MoveLeft();
        Assert.Equal(1, buffer.Caret);
    }

    [Fact]
    public void LineMoves_AtLineBoundaries()
    {
        var buffer = new TextBuffer("가\n나다\n라");

        buffer.MoveTo(2);
        buffer.MoveLineStart();
        Assert.Equal(2, buffer.Caret);

        buffer.MoveTo(1);
        buffer.MoveLineEnd();
        Assert.Equal(1, buffer.Caret);
        buffer.MoveLineStart();
        Assert.Equal(0, buffer.Caret);

        buffer.MoveTo(6);
        buffer.MoveLineStart(extend: true);
        Assert.Equal("라", buffer.SelectedText);
        buffer.MoveLineEnd();
        Assert.Equal(6, buffer.Caret);
    }

    [Fact]
    public void Backspace_AtLineStart_JoinsLines()
    {
        var buffer = new TextBuffer("S\nO");
        buffer.MoveTo(2);

        buffer.Backspace();

        Assert.Equal("SO", buffer.Text);
        Assert.Equal(1, buffer.Caret);
    }
}
