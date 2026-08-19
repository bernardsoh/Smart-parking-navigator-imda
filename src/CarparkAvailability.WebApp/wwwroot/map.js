window.mapFunctions = (() => {
    let map;
    let markersLayer;

    function initMap(elementId, lat, lon) {
        const element = document.getElementById(elementId);
        if (!element || typeof L === "undefined") {
            return;
        }

        if (!map) {
            map = L.map(elementId).setView([lat, lon], 15);
            L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                maxZoom: 19,
                attribution: "&copy; OpenStreetMap contributors"
            }).addTo(map);
            markersLayer = L.layerGroup().addTo(map);
        } else {
            map.setView([lat, lon], 15);
        }
    }

    function updateMarkers(carparks) {
        if (!map || !markersLayer) {
            return;
        }

        markersLayer.clearLayers();
        const bounds = [];

        (carparks || []).forEach(carpark => {
            const marker = L.marker([carpark.latitude, carpark.longitude]).addTo(markersLayer);
            marker.bindPopup(`<strong>${carpark.carparkNo}</strong><br/>${carpark.address}<br/>Available lots: ${carpark.availableLots}<br/>Freshness: ${carpark.freshness}`);
            bounds.push([carpark.latitude, carpark.longitude]);
        });

        if (bounds.length > 0) {
            map.fitBounds(bounds, { padding: [24, 24] });
        }
    }

    return { initMap, updateMarkers };
})();
