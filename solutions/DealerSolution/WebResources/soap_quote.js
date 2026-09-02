// Main onLoad function for the form
function onLoad(executionContext) {
    togglePaymentType(executionContext);
    registerEvents(executionContext);
}

// Register all event handlers
function registerEvents(executionContext) {
    const formContext = executionContext.getFormContext();
    const paymentTypeAttribute = formContext.getAttribute("soap_paymenttype");

    if (paymentTypeAttribute) {
        paymentTypeAttribute.addOnChange(function () {
            togglePaymentType(executionContext);
        });
    }
}

// Toggle fields based on the selected payment type value
function togglePaymentType(executionContext) {
    const formContext = executionContext.getFormContext();
    const paymentTypeAttribute = formContext.getAttribute("soap_paymenttype");

    if (!paymentTypeAttribute) {
        return;
    }

    const paymentTypeValue = paymentTypeAttribute.getValue();

    // Define all fields that might be shown/hidden
    const allFields = [
        "soap_salestaxrate",
        "soap_leasingcompanyid",
        "soap_leasetypeid",
        "soap_leaserateid",
        "soap_leasefactor"
    ];

    // Hide all fields first
    allFields.forEach(fieldName => {
        const control = formContext.getControl(fieldName);
        if (control) {
            control.setVisible(false);
        }
    });

    if (paymentTypeValue === 1) {
        // Show purchase fields
        const purchaseFields = ["soap_salestaxrate"];
        purchaseFields.forEach(fieldName => {
            const control = formContext.getControl(fieldName);
            if (control) {
                control.setVisible(true);
            }
        });
    } else if (paymentTypeValue === 2) {
        // Show leasing fields
        const leasingFields = [
            "soap_leasingcompanyid",
            "soap_leasetypeid",
            "soap_leaserateid",
            "soap_leasefactor"
        ];
        leasingFields.forEach(fieldName => {
            const control = formContext.getControl(fieldName);
            if (control) {
                control.setVisible(true);
            }
        });
    }
}