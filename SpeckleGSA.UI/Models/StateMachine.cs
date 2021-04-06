using SpeckleGSA.UI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeckleGSA.UI.Models
{
  public class StateMachine
  {
    //Overall state - the main output of this class
    public AppState State { get => stateFns.Keys.FirstOrDefault(k => stateFns[k]()); }
    public bool IsOccupied { get => (streamState == StreamState.Active || streamState == StreamState.Pending || fileState == FileState.Loading || fileState == FileState.Saving); }

    #region private_state_variables
    //Don't need previous state as the information embodied in such a variable is stored in the variables below
    private bool loggedIn = false;
    private bool prevFileLoaded = false;
    private StreamState streamState = StreamState.None;
    private FileState fileState = FileState.None;
    private StreamMethod streamMethod = StreamMethod.None;
    private Direction streamDirection = Direction.None;
    private StreamContent streamContent = StreamContent.None;
    #endregion

    #region private_condition_fns

    private bool Is(StreamState ss, Direction dir, StreamContent cont) => (streamState == ss && streamDirection == dir && streamContent == cont);
    private bool Is(FileState fs, StreamState ss, Direction dir, StreamContent cont) => (fileState == fs && streamState == ss && streamDirection == dir && streamContent == cont);
    private void Set(StreamState ss, Direction dir, StreamContent cont)
    {
      streamState = ss;
      streamDirection = dir;
      streamContent = cont;
    }
    #endregion

    private readonly Dictionary<AppState, Func<bool>> stateFns;

    public StateMachine()
    {
      stateFns = new Dictionary<AppState, Func<bool>>()
      {
        { AppState.NotLoggedIn,                () => !loggedIn && !IsOccupied && fileState != FileState.Loaded },
        { AppState.NotLoggedInLinkedToGsa,     () => !loggedIn && !IsOccupied && fileState == FileState.Loaded },
        { AppState.ActiveLoggingIn,            () => !loggedIn && Is(StreamState.Active, Direction.Receiving, StreamContent.Accounts) },
        { AppState.ActiveRetrievingStreamList, () => !loggedIn && Is(StreamState.Active, Direction.Receiving, StreamContent.StreamInfo) },
        { AppState.LoggedInNotLinkedToGsa,     () => loggedIn && fileState != FileState.Loaded && !IsOccupied },
        { AppState.OpeningFile,                () => fileState == FileState.Loading },
        { AppState.ReceivingWaiting,           () => loggedIn && Is(FileState.Loaded, StreamState.Pending, Direction.Receiving, StreamContent.Objects) },
        { AppState.ActiveReceiving,            () => loggedIn && Is(FileState.Loaded, StreamState.Active, Direction.Receiving, StreamContent.Objects) },
        { AppState.SendingWaiting,             () => loggedIn && Is(FileState.Loaded, StreamState.Pending, Direction.Sending, StreamContent.Objects) },
        { AppState.ActiveSending,              () => loggedIn && Is(FileState.Loaded, StreamState.Active, Direction.Sending, StreamContent.Objects) },
        { AppState.Ready,                      () => loggedIn && Is(FileState.Loaded, StreamState.None, Direction.None, StreamContent.None) },
        { AppState.SavingFile,                 () => Is(FileState.Saving, StreamState.None, Direction.None, StreamContent.None) },
        { AppState.ActiveRenamingStream,       () => loggedIn && Is(StreamState.Active, Direction.Sending, StreamContent.StreamInfo) }
      };
    }

    public void StartedLoggingIn()
    {
      if (!loggedIn && !IsOccupied)
      {
        //No need to change the state of the file, nor the loggedIn boolean as that serves as the "previous" logged-in state, useful if the logging in is cancelled
        Set(StreamState.Active, Direction.Receiving, StreamContent.Accounts);
      }
    }

    public void CancelledLoggingIn()
    {
      if (Is(StreamState.Active, Direction.Receiving, StreamContent.Accounts))
      {
        //No need to set the loggedIn value as that will essentially revert to how it was
        //No need to change the state of the file
        Set(StreamState.None, Direction.None, StreamContent.None);
      }
    }

    public void LoggedIn()
    {
      if (Is(StreamState.Active, Direction.Receiving, StreamContent.Accounts))
      {
        loggedIn = true;
        //No need to change the state of the file
        Set(StreamState.None, Direction.None, StreamContent.None);
      }
    }

    public void EnteredReceivingMode(StreamMethod streamMethod) => EnteredStreamingMode(Direction.Receiving, streamMethod);

    //This same method is called at the end of an active receive event and the end of being in the state of continuous receive
    public void StoppedReceiving() => StoppedStreaming(Direction.Receiving);

    public void EnteredSendingMode(StreamMethod streamMethod) => EnteredStreamingMode(Direction.Sending, streamMethod);

    //This same method is called at the end of an active send event and the end of being in the state of continuous send
    public void StoppedSending() => StoppedStreaming(Direction.Sending);

    public void StartedUpdatingStreams()
    {
      if (loggedIn && !IsOccupied)
      {
        //File state isn't relevant here
        Set(StreamState.Active, Direction.Receiving, StreamContent.StreamInfo);
      }
    }

    public void StoppedUpdatingStreams()
    {
      if (Is(StreamState.Active, Direction.Receiving, StreamContent.StreamInfo))
      {
        Set(StreamState.None, Direction.None, StreamContent.None);
      }
    }


    public void StartedSavingFile()
    {
      if (fileState == FileState.Loaded && !IsOccupied)
      {
        //logged in state isn't strictly relevant here
        this.fileState = FileState.Saving;
      }
    }

    public void StoppedSavingFile()
    {
      if (fileState == FileState.Saving)
      {
        fileState = FileState.None;
      }
    }

    public void StartedOpeningFile()
    {
      if (!IsOccupied)
      {
        fileState = FileState.Loading;
      }
    }

    public void OpenedFile()
    {
      if (fileState == FileState.Loading)
      {
        fileState = FileState.Loaded;
        prevFileLoaded = true;
      }
    }

    public void CancelledOpeningFile()
    {
      if (fileState == FileState.Loading)
      {
        fileState = prevFileLoaded ? FileState.Loaded : FileState.None;
      }
    }

    public void StartedRenamingStream()
    {
      if (loggedIn && fileState == FileState.Loaded && !IsOccupied)
      {
        Set(StreamState.Active, Direction.Sending, StreamContent.StreamInfo);
      }
    }

    public void StoppedRenamingStream()
    {
      if (Is(StreamState.Active, Direction.Sending, StreamContent.StreamInfo))
      {
        Set(StreamState.None, Direction.None, StreamContent.None);
      }
    }

    private void EnteredStreamingMode(Direction direction, StreamMethod streamMethod)
    {
      if (loggedIn && fileState == FileState.Loaded && !IsOccupied)
      {
        this.streamMethod = streamMethod;
        //StreamState won't be None as that is filtered out by the IsOccupied check
        Set(StreamState.Active, direction, StreamContent.Objects);
      }
    }

    private void StoppedStreaming(Direction direction)
    {
      if (Is(StreamState.Active, direction, StreamContent.Objects))
      {
        if (streamMethod == StreamMethod.Single)
        {
          Set(StreamState.None, Direction.None, StreamContent.None);
        }
        else if (streamMethod == StreamMethod.Continuous)
        {
          Set(StreamState.Pending, direction, StreamContent.Objects);
        }
      }
      else if (Is(StreamState.Pending, direction, StreamContent.Objects))
      {
        Set(StreamState.None, Direction.None, StreamContent.None);
      }
    }

    #region private_enums
    private enum Direction
    {
      None,
      Sending,
      Receiving
    }

    private enum StreamContent
    {
      None,
      Accounts,
      StreamInfo,
      Objects
    }

    private enum StreamState
    {
      None,
      Active,
      Pending
    }

    private enum FileState
    {
      None,
      Loading,
      Saving,
      Loaded
    }
    #endregion
  }
}
