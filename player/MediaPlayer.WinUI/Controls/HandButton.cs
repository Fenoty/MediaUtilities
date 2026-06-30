using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace MediaPlayer.Controls;

public class HandButton : Button
{
    public HandButton()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}
