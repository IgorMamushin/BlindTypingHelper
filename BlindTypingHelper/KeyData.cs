using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BlindTypingHelper;

public class KeyData : INotifyPropertyChanged
{
    private char? _character;
    private char? _shiftCharacter;
    private bool _isPressed;
    private bool _shiftPressed;

    public char? Letter => _shiftPressed ? _shiftCharacter : _character;

    public bool IsPressed
    {
        get => _isPressed;
        set
        {
            if (_isPressed != value)
            {
                _isPressed = value;
                OnPropertyChanged();
            }
        }
    }

    public bool Unavailable { get; set; }

    public void SetCharacter(char? character, char? shiftCharacter)
    {
        _character = character;
        _shiftCharacter = shiftCharacter;
        OnPropertyChanged(nameof(Letter));
    }

    public void ShiftPressed(bool isPressed)
    {
        if (_shiftPressed != isPressed)
        {
            _shiftPressed = isPressed;
            OnPropertyChanged(nameof(Letter));
            OnPropertyChanged(nameof(Unavailable));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}