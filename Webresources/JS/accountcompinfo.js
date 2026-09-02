// Main onLoad function for the form
function onLoad(executionContext) {
    // Initial toggle based on current value
    toggleSectionsByBusinessPillar(executionContext);

    // Register event handler for future changes
    registerBusinessPillarChangeHandler(executionContext);
}

// Register the event handlers
function registerBusinessPillarChangeHandler(executionContext) {
    try {
        const formContext = executionContext.getFormContext();
        const businessPillarAttribute = formContext.getAttribute("soap_businesspillar");

        if (businessPillarAttribute) {
            businessPillarAttribute.addOnChange(function () {
                toggleSectionsByBusinessPillar(executionContext);
            });
        }

        const purchaseTypeAttribute = formContext.getAttribute("soap_purchasetype");

        if (purchaseTypeAttribute) {
            purchaseTypeAttribute.addOnChange(function () {
                toggleFieldsByPurchaseType(executionContext);
            });
        }
    } catch (error) {
        console.error("Error in registerBusinessPillarChangeHandler: ", error.message);
    }
}

// Toggle sections based on the selected business pillar value
function toggleSectionsByBusinessPillar(executionContext) {
    try {
        const formContext = executionContext.getFormContext();
        const businessPillarAttribute = formContext.getAttribute("soap_businesspillar");

        if (!businessPillarAttribute) {
            return;
        }

        // Get the selected value (returns null if no option is selected)
        const businessPillarValue = businessPillarAttribute.getValue();

        // Define the mapping between business pillar values and section names
        const sectionMapping = {
            1: "gen_oe",
            2: "gen_mnsit",
            3: "gen_telecom",
            4: "gen_camera",
            5: "gen_other"
        };

        // Get all section names from the mapping
        const allSections = Object.values(sectionMapping);

        // Hide all sections first
        allSections.forEach(sectionName => {
            const section = formContext.ui.tabs.get("gen").sections.get(sectionName);
            if (section) {
                section.setVisible(false);
            }
        });

        // Show only the relevant section if a value is selected
        if (businessPillarValue !== null && businessPillarValue !== undefined) {
            const sectionToShow = sectionMapping[businessPillarValue];
            if (sectionToShow) {
                const section = formContext.ui.tabs.get("gen").sections.get(sectionToShow);
                if (section) {
                    section.setVisible(true);
                } else {
                    console.warn(`Section ${sectionToShow} not found on the form`);
                }
            }
        }
    } catch (error) {
        console.error("Error in toggleSectionsByBusinessPillar: ", error.message);
    }
}

// Toggle fields based on the selected purchase type value
function toggleFieldsByPurchaseType(executionContext) {
    try {
        const formContext = executionContext.getFormContext();
        const purchaseTypeAttribute = formContext.getAttribute("soap_purchasetype");

        if (!purchaseTypeAttribute) {
            return;
        }

        // Get the selected value (returns null if no option is selected)
        const purchaseTypeValue = purchaseTypeAttribute.getValue();

        // Define all fields that might be shown/hidden
        const allFields = [
            "soap_purchaseprice",
            "soap_purchasedate",
            "soap_leasingcompany",
            "soap_paymentamount",
            "soap_servicepayment",
            "soap_leaseterm",
            "soap_startdate",
            "soap_enddate"
        ];

        // Hide all fields first
        allFields.forEach(fieldName => {
            const control = formContext.getControl(fieldName);
            if (control) {
                control.setVisible(false);
            }
        });

        // Show fields based on purchase type
        if (purchaseTypeValue === 748110000) {
            // Show purchase fields
            const purchaseFields = ["soap_purchaseprice", "soap_purchasedate"];
            purchaseFields.forEach(fieldName => {
                const control = formContext.getControl(fieldName);
                if (control) {
                    control.setVisible(true);
                }
            });
        } else if (purchaseTypeValue === 748110001) {
            // Show leasing fields
            const leasingFields = [
                "soap_leasingcompany",
                "soap_paymentamount",
                "soap_servicepayment",
                "soap_leaseterm",
                "soap_startdate",
                "soap_enddate"
            ];
            leasingFields.forEach(fieldName => {
                const control = formContext.getControl(fieldName);
                if (control) {
                    control.setVisible(true);
                }
            });
        }
        // For any other value or null, all fields remain hidden

    } catch (error) {
        console.error("Error in toggleFieldsByPurchaseType: ", error.message);
    }
}