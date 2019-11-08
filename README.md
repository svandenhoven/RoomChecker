# RoomChecker

This application gives an overview of Rooms in your Office 365 and shows it availability. Using the bGrid API the occupancy of a room can be seen.
It gives a fast overview if the rooms are available

You need to have a application registred in Azure AD with GraphAPI permission "Calendars.Read.Shared" enabled

This documentation will be extended.

![alt text](/roomcheck.jpg "Screenshot of Roomchecker")

This is based on an config file with the id of tenant that needs the have the following form

```json
{
  "bGridConfig": {
    "bGridUser": "...",
    "bGridPW": "...",
    "bGridEndPoint": "..."
  },
	"pBIConfig":
	{
		"workspaceId": "...",
		"reportId": "..."
	},
	"KnownAssets":[
		{
			"id" : 1, 
			"name": "wheelchair",
			"type" : "wheelchair.png"
		},
				{
			"id" : 2, 
			"name": "Post Trolley",
			"type" : "postcar.jpg"
		},
		{
			"id" : 3, 
			"name": "Cleaning",
			"type" : "cleantrolley.jpg"
		},
		{
			"id" : 4, 
			"name": "Chainsaw",
			"type" : "chainsaw.jpg"
		}
	],
  "Rooms": [
      {
        "id": "room1@contoso.com",
        "hasMailBox": true,
        "available": true,
        "name": "room1",
        "floor": 1,
        "roomType": "meet",
        "type": "internal",
        "nodes": [
          {
            "id": "123"
          },
          {
            "id": "345"
          }
        ],
        "capacity": 6,
        "audioVideo": "AV"
      },
      {
        "id": "room2@contoso.com",
        "hasMailBox": true,
        "available": true,
        "name": "room2",
        "floor": 1,
        "roomType": "meet",
        "type": "external",
        "nodes": [
          {
            "id": "456"
          },
          {
            "id": "789"
          }
        ],
        "capacity": 8,
        "audioVideo": "AV"
      }
    ]
  }
```
