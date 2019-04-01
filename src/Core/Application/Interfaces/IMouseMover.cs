namespace Application.Interfaces
{
    public interface IMouseMover
    {
        void ScrollUp();
        void ScrollDown();
        void MoveTo(int x, int y);
        void Click(int x, int y);
    }
}