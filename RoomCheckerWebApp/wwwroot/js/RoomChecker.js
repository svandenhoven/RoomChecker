$(document).ready(function () {
    var dt = document.getElementById('checkTime');
    dt.value = getDateString();
    SetStatus();
});

function toggleFloorButton(b) {
    if (b.className.startsWith('btn btn-default')) {
        b.className = 'btn btn-primary';
    }
    else {
        b.className = 'btn btn-default';
    }
}

function getDateString() {
    var dateobj = new Date();
    var year = dateobj.getFullYear();
    var month = ("0" + (dateobj.getMonth() + 1)).slice(-2);
    var date = ("0" + dateobj.getDate()).slice(-2);
    var hours = ("0" + dateobj.getHours()).slice(-2);
    var minutes = ("0" + dateobj.getMinutes()).slice(-2);
    return year + "-" + month + "-" + date + "T" + hours + ":" + minutes;
}

function SetStatus() {
    var dt = document.getElementById('checkTime');
    dt.value = getDateString();
    $('#timeTitle').text('Meeting Rooms available at ' + n(new Date().getHours()) + ":" + n(new Date().getMinutes()));

    rooms.forEach(function (room) {
        GetRoomStatus(room);
    });

}

function SetStatusOnDate() {
    var dt = document.getElementById('checkTime');
    $('#timeTitle').text('Rooms available on ' + dt.value.slice(0, 10) + ' at ' + dt.value.slice(11, 16));

    rooms.forEach(function (room) {
        GetRoomStatusOnDate(room, false);
    });

}

function n(n) {
    return n > 9 ? "" + n : "0" + n;

}

function GetRoomStatusOnDate(id, showbGrid) {
    var cell = document.getElementById('item_' + id);
    cell.style.backgroundColor = "";
    $('#status_' + id).text('Refreshing');
    $('#planning_' + id).text('...');
    for (var i = 8; i < 19; i++) {
        var cellC = document.getElementById(id + '-h-' + i);
        cellC.className = 'unknown';
    }

    var dt = document.getElementById('checkTime').value;

    $.getJSON("/Home/GetRoomStatusOnDate",
        {
            roomId: id,
            dateTime: dt,
            type: roomType
        },
        function (data) {
            var itemCell = document.getElementById('item_' + data.name);
            if (data.hasMailBox) {
                if (data.available) {

                    $('#status_' + id).text('Available');
                    if (data.freeUntil !== '0001-01-01T00:00:00')
                        $('#planning_' + id).text('until ' + data.freeUntil.substring(11, 16));
                    else {
                        if (data.freeAt !== '0001-01-01T00:00:00') {
                            $('#planning_' + id).text('Free at ' + data.freeAt.substring(11, 16));
                        }
                        else {
                            $('#planning_' + id).text('rest of day');
                        }
                    }
                    itemCell.style.backgroundColor = "lightgreen";
                }
                else {
                    $('#status_' + id).text('Reserved');
                    if (data.freeAt !== '0001-01-01T00:00:00')
                        $('#planning_' + id).text('Free at ' + data.freeAt.substring(11, 16));
                    else
                        $('#planning_' + id).text('rest of day');
                    itemCell.style.backgroundColor = "pink";
                }
            }
            else {
                $('#status_' + id).text('Free seating');
                $('#planning_' + id).text('');
                itemCell.style.backgroundColor = "lightblue";
            }

            var label = document.getElementById('occupied_' + data.name);
            if (showbGrid) {

                switch (data.occupied) {
                    case 0:
                        label.innerText = 'Free ' + data.temperature.toFixed(1) + "°C";
                        label.className = 'label label-success';
                        label.style.visibility = "visible";
                        break;
                    case 1:
                        label.innerText = 'Occupied ' + data.temperature.toFixed(1) + "°C";
                        label.className = 'label label-warning';
                        label.style.visibility = "visible";
                        break;
                    case 2:
                        label.innerText = 'Occupied ' + data.temperature.toFixed(1) + "°C";
                        label.className = 'label label-danger';
                        label.style.visibility = "visible";
                        break;
                    default:
                        label.innerText = data.temperature.toFixed(1) + "°C";
                        label.className = 'label label-default';
                        label.style.visibility = "hidden";
                        break;
                }
            }
            else {
                label.className = 'label label-default';
                label.style.visibility = "hidden";
            }

            for (var i = 8; i < 19; i++) {
                var cell = document.getElementById(id + '-h-' + i);
                if (data.daySchedule[i] === 1) {
                    cell.className = 'notfree';
                }
                if (data.daySchedule[i] === 0) {
                    cell.className = 'free';
                }
            }
        });
}


function GetRoomStatus(id) {
    var dt = document.getElementById('checkTime');
    dt.value = getDateString();
    GetRoomStatusOnDate(id, true);
}