window.AdminTime = {
    format(value, missing = "—") {
        if (!value) return missing;
        const source = String(value);
        const hasOffset = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(source);
        const instant = new Date(hasOffset ? source : source + "Z");
        if (Number.isNaN(instant.getTime())) return missing;
        return new Date(instant.getTime() - 7 * 60 * 60 * 1000)
            .toISOString().slice(0, 16).replace("T", " ");
    },
    today() {
        return new Date(Date.now() - 7 * 60 * 60 * 1000).toISOString().slice(0, 10);
    }
};
