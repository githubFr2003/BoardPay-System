// wwwroot/js/site.js

document.addEventListener('DOMContentLoaded', () => {
    // --- Element References ---
    const loginForm = document.getElementById('login-form');
    const loginError = document.getElementById('login-error');
    // Logout buttons are handled by form submission now, JS listener removed.

    // --- Form/Modal/Message References (Check if they exist) ---
    const addBuildingFormContainer = document.getElementById('add-building-form-container');
    const buildingForm = document.getElementById('building-form');
    const buildingFormMessage = document.getElementById('building-form-message'); // Keep for potential server-side feedback display
    const addTenantForm = document.getElementById('add-tenant-form');
    const addTenantMessage = document.getElementById('add-tenant-message'); // Keep for potential server-side feedback display
    const meterReadingForm = document.getElementById('meter-reading-form');
    const addReadingMessage = document.getElementById('add-reading-message'); // Keep for potential server-side feedback display
    const updateFeesModal = document.getElementById('update-fees-modal');
    const updateFeesForm = document.getElementById('update-fees-form');
    const updateFeesMessage = document.getElementById('update-fees-message'); // Keep for potential server-side feedback display
    const gcashBtn = document.querySelector('#tenant-dashboard #t-payment button.btn-success'); // Assumes Tenant dashboard view exists
    const gcashMessage = document.getElementById('gcash-message');

    // --- Helper: Show Message ---
    // This can still be useful for client-side validation messages before submit,
    // or potentially displaying messages returned from the server if you implement AJAX later.
    function showMessage(element, text, type = 'success', duration = 3000) {
        if (!element) return;
        element.textContent = text;
        element.className = `message ${type}`; // Reset classes
        element.style.display = 'block';
        if (duration > 0) {
            if (element.timeoutId) clearTimeout(element.timeoutId);
            element.timeoutId = setTimeout(() => {
                element.style.display = 'none';
                element.timeoutId = null;
            }, duration);
        }
    }
    window.showMessage = showMessage; // Make global if needed elsewhere


    // --- Login Form Handling ---
    if (loginForm) {
        loginForm.addEventListener('submit', (e) => {
            // REMOVED: e.preventDefault();
            // Let the form submit to the server via standard POST.

            // Client-side validation can still happen BEFORE the submit occurs.
            if (loginError) loginError.style.display = 'none';

            const usernameInput = document.getElementById('username'); // Use specific ID if needed
            const passwordInput = document.getElementById('password');
            const roleSelect = document.getElementById('role');

            const username = usernameInput?.value;
            const password = passwordInput?.value;
            const role = roleSelect?.value;

            // Basic client-side check: Ensure fields are not empty before allowing submit.
            if (!username || !password || !role) {
                // Prevent submission ONLY if basic client validation fails.
                e.preventDefault();
                if (loginError) showMessage(loginError, "Please fill in all fields.", 'error', 0);
                return; // Stop further execution
            }

            // The actual login logic, credential validation, and redirection
            // will now be handled by the server-side AccountController.Login POST action.
            // The JS simulation logic (checking role, window.location.href) is removed.
        });
    }

    // --- Logout ---
    // REMOVED: logoutBtns.forEach(...) listener.
    // Logout should be handled by submitting a form containing the logout button
    // directly to the server's Logout action. Ensure your logout button is
    // inside a <form method="post" asp-action="Logout" asp-controller="Account">
    // with @Html.AntiForgeryToken().


    // --- Landlord Page Specific Logic ---

    // Manage Buildings Form Toggle (UI Logic - Keep as is)
    window.showAddBuildingForm = () => {
        if (buildingForm) buildingForm.reset();
        const buildingIdInput = document.getElementById('building-id');
        if (buildingIdInput) buildingIdInput.value = ''; // Reset hidden field if editing
        if (addBuildingFormContainer) addBuildingFormContainer.style.display = 'block';
        if (buildingFormMessage) buildingFormMessage.style.display = 'none'; // Hide old messages
        addBuildingFormContainer?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    };
    window.hideAddBuildingForm = () => {
        if (addBuildingFormContainer) addBuildingFormContainer.style.display = 'none';
    };

    // Building Form Submission
    if (buildingForm) {
        buildingForm.addEventListener('submit', (e) => {
            // REMOVED: e.preventDefault();
            // Let the form submit to the server.
            // Server-side action (e.g., LandlordController.ManageBuildings [HttpPost]
            // or LandlordController.AddBuilding [HttpPost]) will handle saving.
            // Success/error messages should be handled by the server response
            // (e.g., using TempData and displaying it in the view).
            // REMOVED: showMessage(buildingFormMessage, ...) simulation.
            // REMOVED: console.log simulation.
        });
    }

    // Add Tenant Form Submission
    if (addTenantForm) {
        addTenantForm.addEventListener('submit', (e) => {
            // REMOVED: e.preventDefault();
            // Let the form submit to the server (e.g., LandlordController.AddTenant [HttpPost]).
            // Server handles saving and success/error messages.
            // REMOVED: showMessage(addTenantMessage, ...) simulation.
            // REMOVED: console.log simulation.
            // REMOVED: addTenantForm.reset(); // Server redirect/response handles state
        });
    }

    // Meter Reading Form Submission
    if (meterReadingForm) {
        meterReadingForm.addEventListener('submit', (e) => {
            // REMOVED: e.preventDefault();
            // Let the form submit to the server (e.g., LandlordController.MeterReadings [HttpPost]).
            // Server handles saving and success/error messages.
            // REMOVED: showMessage(addReadingMessage, ...) simulation.
            // REMOVED: console.log simulation.
            // REMOVED: meterReadingForm.reset(); // Server redirect/response handles state
        });
    }

    // --- Tenant Detail & Fee Update Modal Logic ---

    // Show Modal (UI Logic - Keep as is, might need AJAX later for dynamic data)
    window.showTenantDetails = (tenantId) => {
        console.log("Opening update fees modal for tenant ID:", tenantId);
        // In a real app, you might fetch CURRENT fee overrides via AJAX here
        // to pre-populate the modal form if needed. For now, it just opens.

        let tenantName = `Tenant ${tenantId}`; // Placeholder, get real name from element or data attribute
        // Example: Find the name in the table row corresponding to tenantId
        const tenantRow = document.querySelector(`tr[data-tenant-id="${tenantId}"]`);
        if (tenantRow) {
            const nameCell = tenantRow.querySelector('td:first-child'); // Assuming name is first cell
            if (nameCell) tenantName = nameCell.textContent;
        }


        const updateTenantIdInput = document.getElementById('update-fees-tenant-id');
        const updateTenantNameHeader = document.getElementById('update-fees-tenant-name');

        if (updateTenantIdInput) updateTenantIdInput.value = tenantId; // Set the ID for the form
        if (updateTenantNameHeader) updateTenantNameHeader.textContent = `Update Fees for ${tenantName}`;

        if (updateFeesModal) {
            updateFeesForm?.reset(); // Clear previous entries
            const msgElem = document.getElementById('update-fees-message');
            if (msgElem) msgElem.style.display = 'none'; // Hide old messages
            updateFeesModal.style.display = 'block';
        } else {
            console.error("Update Fees Modal not found on this page.");
        }
    };

    // Show/Close Modal Functions (UI Logic - Keep as is)
    window.showUpdateFeesModal = () => { // Can be called directly if needed
        if (updateFeesModal) {
            updateFeesForm?.reset();
            const msgElem = document.getElementById('update-fees-message');
            if (msgElem) msgElem.style.display = 'none';
            updateFeesModal.style.display = 'block';
        }
    };
    window.closeUpdateFeesModal = () => {
        if (updateFeesModal) updateFeesModal.style.display = 'none';
    };

    // Fee Update Form Submission (Inside Modal)
    if (updateFeesForm) {
        updateFeesForm.addEventListener('submit', (e) => {
            // REMOVED: e.preventDefault();
            // Let the form submit to the server (e.g., LandlordController.UpdateTenantFees [HttpPost]).
            // The hidden input 'update-fees-tenant-id' will carry the tenant ID.
            // Server handles saving, notification logic, and success/error messages.
            // REMOVED: showMessage(updateFeesMessage, ...) simulation.
            // REMOVED: console.log simulation.
            // Modal closing might be handled server-side (redirect) or you could
            // add JS to close it based on server feedback if using AJAX later.
        });
    }

    // --- Tenant Page Specific Logic ---
    // GCash Button click (assuming this INITIATES something, might need AJAX or form post later)
    if (gcashBtn) {
        gcashBtn.addEventListener('click', () => {
            // This button is NOT submitting a form in the original code.
            // If it SHOULD submit a form to initiate payment server-side,
            // change the button to type="submit" and wrap it in a form.
            // If it triggers a client-side SDK or redirects, the JS is needed.
            // Keeping simulation for now, replace with actual logic.
            showMessage(gcashMessage, 'GCash payment initiated (simulation).');
            console.log("Simulating GCash payment initiation");
            // ** REAL APP: Replace with actual payment initiation logic (e.g., redirect, SDK call) **
        });
    }

    // --- General ---
    // Close modal clicking outside (UI Logic - Keep as is)
    window.onclick = function (event) {
        if (updateFeesModal && event.target == updateFeesModal) {
            closeUpdateFeesModal();
        }
    }

}); // End DOMContentLoaded