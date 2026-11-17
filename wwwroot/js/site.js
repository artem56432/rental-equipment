document.addEventListener('DOMContentLoaded', () => {
    const startDateInput = document.querySelector('input[name="startDate"]');
    const endDateInput = document.querySelector('input[name="endDate"]');

    if (!startDateInput || !endDateInput) {
        return;
    }

    function updateMinimumReturnDate() {
        if (startDateInput.value) {
            endDateInput.min = startDateInput.value;
        }
    }

    document.querySelectorAll('input[type="date"]').forEach(input => {
        input.addEventListener('change', updateMinimumReturnDate);
    });
});
