using System.Collections.Generic;

public class Trackable<T> where T: unmanaged
{
    readonly unsafe T* _val;
    T _prevVal;
    public unsafe Trackable(T val)
    {
        this._val = &val;
        _prevVal = val;
    }

    public unsafe bool Dirty()
    {
        bool tf = EqualityComparer<T>.Default.Equals(*_val, _prevVal);
        _prevVal = *_val;
        return tf;
    }


}
