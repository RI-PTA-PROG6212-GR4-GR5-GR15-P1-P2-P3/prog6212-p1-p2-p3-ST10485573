// Show a loading spinner on form submit
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('form').forEach(function (form) {
        form.addEventListener('submit', function () {
            var spinner = document.getElementById('spinner');
            if (spinner) spinner.classList.add('active');
        });
    });
});