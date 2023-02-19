using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoomsTag
{

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;
              
                Transaction tran = new Transaction(doc, "Rooms №");
                tran.Start();
                // Создание нового образца класса - create a new instance of class data
                CreateNumber numb = new CreateNumber(commandData);

                tran.Commit();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // If there are something wrong, give error information and return failed
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    public class CreateNumber
    {
        UIApplication modRevit;

        List<Room> modRooms = new List<Room>();    // Коллекция хранит все помещения в проекте - a list to store all rooms in the project
        List<RoomTag> modRoomsTag = new List<RoomTag>(); // Коллекция хранит все теги (марки) помещения в проекте - a list to store all room tags
        List<Room> modRoomsWithTag = new List<Room>();   // a list to store all rooms with tag
        List<Room> modRoomsWithoutTag = new List<Room>(); // a list to store all rooms without tag

        public ReadOnlyCollection<RoomTag> RoomTags
        {
            get
            {
                return new ReadOnlyCollection<RoomTag>(modRoomsTag);
            }
        }

        public ReadOnlyCollection<Room> RoomsWithoutTag
        {
            get
            {
                return new ReadOnlyCollection<Room>(modRoomsWithoutTag);
            }
        }

        public CreateNumber(ExternalCommandData commandData)
        {
            modRevit = commandData.Application;

            // get all the rooms and room tags in the project
            GetAllRoomsAndTags();

            // find out the rooms that without room tag
            ClassifyRooms();

            // создание марки
            CreateTags();
        }

        // find out the rooms that without room tag
        private void ClassifyRooms()
        {
            modRoomsWithoutTag.Clear();
            modRoomsWithTag.Clear();

            // copy the all the elements in list Rooms to list RoomsWithoutTag
            modRoomsWithoutTag.AddRange(modRooms);

            // get the room id from room tag via room property
            // if find the room id in list RoomWithoutTag,
            // add it to the list RoomWithTag and delete it from list RoomWithoutTag
            foreach (RoomTag tmpTag in modRoomsTag)
            {
                ElementId idValue = tmpTag.Room.Id;
                modRoomsWithTag.Add(tmpTag.Room);

                // search the id for list RoomWithoutTag
                foreach (Room tmpRoom in modRooms)
                {
                    if (idValue == tmpRoom.Id)
                    {
                        modRoomsWithoutTag.Remove(tmpRoom);
                    }
                }
            }
        }

        // get all the rooms and room tags in the project
        private void GetAllRoomsAndTags()
        {
            // get the active document 
            Document document = modRevit.ActiveUIDocument.Document;
            RoomFilter roomFilter = new RoomFilter();
            RoomTagFilter roomTagFilter = new RoomTagFilter();
            LogicalOrFilter orFilter = new LogicalOrFilter(roomFilter, roomTagFilter);

            FilteredElementIterator elementIterator =new FilteredElementCollector(document)
                .WherePasses(orFilter)
                .GetElementIterator();
            elementIterator.Reset();

            // try to find all the rooms and room tags in the project and add to the list
            while (elementIterator.MoveNext())
            {
                object obj = elementIterator.Current;

                // find the rooms, skip those rooms which don't locate at Level yet.
                Room tmpRoom = obj as Room;
                if (null != tmpRoom && null != document.GetElement(tmpRoom.LevelId))
                {
                    modRooms.Add(tmpRoom);
                    continue;
                }

                // find the room tags
                RoomTag tmpTag = obj as RoomTag;
                if (null != tmpTag)
                {
                    modRoomsTag.Add(tmpTag);
                    continue;
                }
            }
        }

        public void CreateTags()
        {
            try
            {
                foreach (Room tmpRoom in modRoomsWithoutTag)
                {
                    // get the location point of the room
                    LocationPoint locPoint = tmpRoom.Location as LocationPoint;
                    if (null == locPoint)
                    {
                        String roomId = "Room Id:  " + tmpRoom.Id.ToString();
                        String errMsg = roomId + "\r\nОшибка при создании метки (room tag),  " +
                                                   "не могу получить точку нахождения! Создайте помещение";
                        throw new Exception(errMsg);
                    }

                    // create a instance of Autodesk.Revit.DB.UV class
                    UV point = new UV(locPoint.Point.X, locPoint.Point.Y);

                    //create room tag
                    RoomTag tmpTag;
                    tmpTag = modRevit.ActiveUIDocument.Document.Create.NewRoomTag(new LinkElementId(tmpRoom.Id), point, null);
                    if (null != tmpTag)
                    {
                        modRoomsTag.Add(tmpTag);
                    }
                }

                // classify rooms
                ClassifyRooms();

                // display a message box
                TaskDialog.Show("Revit", "Создание меток (room tags) успешно завершено!");
            }
            catch (Exception exception)
            {
                TaskDialog.Show("Revit", exception.Message);
            }
        }
    }
}
