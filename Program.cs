using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ElevatorApp
{

  public enum ElevatorDirection
  {
    Up,
    Down,
    None
  }

  public enum ElevatorState
  {
    Stopped,
    Moving
  }

  public class ElevatorSensor
  {
    public int CurrentFloor { get; set; }
    public ElevatorDirection Direction { get; set; }
    public ElevatorState State { get; set; }
    public bool IsOverweight { get; set; }

    public ElevatorSensor()
    {
      CurrentFloor = 1;
      Direction = ElevatorDirection.None;
      State = ElevatorState.Stopped;
      IsOverweight = false;
    }

  }
  public class FloorRequestButton
  {
    public int FloorNumber { get; private set; }

    public ElevatorDirection Direction { get; private set; }
    public FloorRequestButton(int floorNumber, ElevatorDirection direction)
    {
      FloorNumber = floorNumber;
      Direction = direction;
    }
  }

  public class ElevatorController
  {
    public readonly ElevatorSensor _sensor;
    public readonly List<FloorRequestButton> _outsideRequests;
    public readonly List<int> _insideRequests;
    public readonly HashSet<int> _visitedFloors;
    public readonly string _logFilePath;

    public ElevatorController()
    {
      _sensor = new ElevatorSensor();
      _outsideRequests = new List<FloorRequestButton>();
      _insideRequests = new List<int>();
      _visitedFloors = new HashSet<int>();
      _logFilePath = @".\elevator.txt";
    }

    public void AddOutsideRequest(int floorNumber, ElevatorDirection direction)
    {
      _outsideRequests.Add(new FloorRequestButton(floorNumber, direction));
      Log($"[{DateTime.Now}] Outside floor {floorNumber} {direction} request added.");
    }

    public void AddInsideRequest(int floorNumber)
    {
      _insideRequests.Add(floorNumber);
      Log($"[{DateTime.Now}] Inside floor {floorNumber} request added.");
    }

    // IMPORTANT:
    public void Run()
    {
      while (_outsideRequests.Any() || _insideRequests.Any())
      {
        if (_sensor.IsOverweight)
        {
          if (_insideRequests.Any())
          {
            var nextFloor = GetNextFloorInsideRequested();
            MoveElevatorToFloor(nextFloor);
          }
          else
          {
            Log($"[{DateTime.Now}] Waiting for passengers to exit (overweight).");
            Wait(5000);
          }
        }
        else
        {
          if (_outsideRequests.Any())
          {
            var nextFloor = GetNextFloorOutsideRequested();
            MoveElevatorToFloor(nextFloor);
          }
          else
          {
            if (_insideRequests.Any())
            {
              var nextFloor = GetNextFloorInsideRequested();
              MoveElevatorToFloor(nextFloor);
            }
          }
        }
      }

      Log($"[{DateTime.Now}] All requests completed. Elevator stopped.");
    }

    private int GetNextFloorOutsideRequested()
    {
      var nextRequest = _outsideRequests
        .Where(req => req.Direction == _sensor.Direction || _sensor.Direction == ElevatorDirection.None)
        .OrderBy(req => Math.Abs(_sensor.CurrentFloor - req.FloorNumber))
        .FirstOrDefault();

      if (nextRequest != null)
      {
        _outsideRequests.Remove(nextRequest);
        return nextRequest.FloorNumber;
      }
      else
      {
        // reverse/toggle Direction
        _sensor.Direction = _sensor.Direction == ElevatorDirection.Up ? ElevatorDirection.Down : ElevatorDirection.Down;
        return GetNextFloorOutsideRequested();
      }

    }

    private int GetNextFloorInsideRequested()
    {
      var nextRequest = _insideRequests
        .OrderBy(i => Math.Abs(_sensor.CurrentFloor - i))
        .First();

      _insideRequests.Remove(nextRequest);

      return nextRequest;
    }

    public void MoveElevatorToFloor(int floorNumber)
    {
      var waitTime = 3000;
      var distance = Math.Abs(_sensor.CurrentFloor - floorNumber);
      var direction = floorNumber > _sensor.CurrentFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
      _sensor.State = ElevatorState.Moving;
      _sensor.Direction = direction;

      while(_sensor.CurrentFloor != floorNumber)
      {
        if (_outsideRequests.Any(req => req.FloorNumber == _sensor.CurrentFloor && req.Direction == _sensor.Direction))
        {
          _outsideRequests.RemoveAll(req => req.FloorNumber == _sensor.CurrentFloor && req.Direction == _sensor.Direction);
          _visitedFloors.Add(_sensor.CurrentFloor);
          Log($"[{DateTime.Now}] Floor {_sensor.CurrentFloor} {direction} request serviced.");
          Wait(1000);
        }
        else if (_sensor.IsOverweight && _insideRequests.Contains(_sensor.CurrentFloor))
        {
          _insideRequests.Remove(_sensor.CurrentFloor);
          _visitedFloors.Add(_sensor.CurrentFloor);
          Log($"[{DateTime.Now}] Inside request for floor {_sensor.CurrentFloor} serviced (overweight).");
          Wait(1000);
        }

        if (_sensor.CurrentFloor < floorNumber)
          _sensor.CurrentFloor++;
        else if (_sensor.CurrentFloor > floorNumber)
          _sensor.CurrentFloor--;

        Log($"[{DateTime.Now}] Passed floor {_sensor.CurrentFloor}.");

        var oppositeRequests = _outsideRequests
          .Where(req => req.FloorNumber > _sensor.CurrentFloor && _sensor.Direction == ElevatorDirection.Down ||
            req.FloorNumber < _sensor.CurrentFloor && _sensor.Direction == ElevatorDirection.Up
          );

        if (oppositeRequests.Any())
        {
          waitTime = Math.Max(waitTime, 5000);
        }

      }

      _sensor.State = ElevatorState.Stopped;
      _visitedFloors.Add(_sensor.CurrentFloor);
      Log($"[{DateTime.Now}] Stopped at floor {_sensor.CurrentFloor}");
      Wait(waitTime);

    }

    private void Wait(int milliseconds)
    {
      Thread.Sleep(milliseconds);
    }

    private void Log(string message)
    {
      if (!File.Exists(_logFilePath))
      {
        FileStream fs = File.Create(_logFilePath);
        fs.Close();
      }
      using var writer = File.AppendText(_logFilePath);
      writer.WriteLine(message);
    }

  }

  class Program
  {
    static void Main(string[] args)
    {
      var elevator = new ElevatorController();

      if (File.Exists(elevator._logFilePath))
      {
        File.Delete(elevator._logFilePath);
      }

      // until user press 'Q'
      while(true)
      {
        Console.Write("Enter floor request from outside(eg 5U, 8D) or inside (eg 2) or 'Q' to end: ");
        var input = Console.ReadLine().ToUpper();
        if (input == "Q")
          break;

        if (input.Length == 1 && int.TryParse(input, out int insideRequest))
        {
          elevator.AddInsideRequest(insideRequest);
        }
        else if (input.Length == 2 && int.TryParse(input[0].ToString(), out int floorNumber))
        {
          var direction = input[1] == 'U' ? ElevatorDirection.Up :
            input[1] == 'D' ? ElevatorDirection.Down :
            ElevatorDirection.None;

          if (direction != ElevatorDirection.None)
            elevator.AddOutsideRequest(floorNumber, direction);
        }
        elevator.Run();

      }
      Console.WriteLine("Visited floors: "+ string.Join(", ", elevator._visitedFloors));

    }
  }
}
